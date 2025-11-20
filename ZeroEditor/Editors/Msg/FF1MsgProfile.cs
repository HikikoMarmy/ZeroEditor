using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Text.RegularExpressions;

namespace ZeroEditor.Editors
{
	sealed class FF1MsgProfile : IMsgProfile
	{
		public string Name => "FF1";
		public int FixedGroupTableOffset => 0x00;

		static bool TryReadUInt( byte[] b, int off, out uint v )
		{
			v = 0;
			if( b == null || off < 0 || off + 4 > b.Length )
				return false;
			v = BitConverter.ToUInt32( b, off );
			return true;
		}

		static int[] ReadPtrTable( byte[] blob, int rootOffset )
		{
			if( !TryReadUInt( blob, rootOffset, out var tableOffsetU ) )
				return Array.Empty<int>();
			int tableOffset = (int)tableOffsetU;
			if( tableOffset <= 0 || tableOffset >= blob.Length )
				return Array.Empty<int>();

			if( TryReadUInt( blob, tableOffset, out var countU ) )
			{
				long maxEntries = ( blob.Length - ( tableOffset + 4 ) ) / 4;
				if( countU > 0 && countU <= int.MaxValue && countU <= maxEntries )
				{
					int count = (int)countU;
					var ptrs = new int[ count ];
					Buffer.BlockCopy( blob, tableOffset + 4, ptrs, 0, count * 4 );
					if( ptrs.All( p => p > 0 && p < blob.Length ) &&
						ptrs.Zip( ptrs.Skip( 1 ), ( a, b ) => a < b ).All( x => x ) )
						return ptrs;
				}
			}

			var list = new List<int>();
			int off = tableOffset;
			int last = -1;
			while( off + 4 <= blob.Length )
			{
				uint u = BitConverter.ToUInt32( blob, off );
				int p = (int)u;
				if( p <= 0 || p >= blob.Length )
					break;
				if( last >= 0 && p <= last )
					break;
				list.Add( p );
				last = p;
				off += 4;
			}
			return list.ToArray();
		}

		public bool TryReadGroupTable( byte[] blob, int offset, out int[] groupStarts )
		{
			groupStarts = Array.Empty<int>();
			var ptrs = ReadPtrTable( blob, offset );
			if( ptrs.Length == 0 )
				return false;
			groupStarts = new[] { 0 };
			return true;
		}

		public IMsgDocument Parse( byte[] blob )
		{
			var doc = new MsgDocument();
			if( blob == null || blob.Length < 8 )
				return doc;

			var ptrs = ReadPtrTable( blob, FixedGroupTableOffset );
			if( ptrs.Length == 0 )
				return doc;

			var caps = new int[ ptrs.Length ];
			for( int i = 0; i < ptrs.Length; i++ )
				caps[ i ] = ( i + 1 < ptrs.Length ) ? ptrs[ i + 1 ] : blob.Length;

			var codec = CreateCodec();
			var grp = new MsgGroup { GroupIndex = 0 };

			for( int id = 0; id < ptrs.Length; id++ )
			{
				int start = ptrs[ id ];
				int end = caps[ id ];
				if( start <= 0 || start >= end || end > blob.Length )
				{
					grp.Messages.Add( new MsgMessage { GroupIndex = 0, MessageIndex = id } );
					continue;
				}

				var msg = new MsgMessage { GroupIndex = 0, MessageIndex = id };
				int cur = start;
				for( int i = start; i < end; i++ )
				{
					if( blob[ i ] == 0xFF )
					{
						int len = i - cur;
						if( len > 0 )
						{
							var pageBytes = new byte[ len ];
							Buffer.BlockCopy( blob, cur, pageBytes, 0, len );
							msg.Pages.Add( new MsgPage
							{
								GroupIndex = 0,
								MessageIndex = id,
								PageIndex = msg.Pages.Count,
								Raw = pageBytes,
								Text = codec.DecodePage( pageBytes )
							} );
						}
						cur = i + 1;
					}
				}
				if( cur < end )
				{
					var pageBytes = new byte[ end - cur ];
					Buffer.BlockCopy( blob, cur, pageBytes, 0, pageBytes.Length );
					msg.Pages.Add( new MsgPage
					{
						GroupIndex = 0,
						MessageIndex = id,
						PageIndex = msg.Pages.Count,
						Raw = pageBytes,
						Text = codec.DecodePage( pageBytes )
					} );
				}

				grp.Messages.Add( msg );
			}

			doc.MutableGroups.Add( grp );
			return doc;
		}

		public byte[] Save( IMsgDocument doc )
		{
			if( doc is not MsgDocument md )
				throw new ArgumentException( "Unexpected doc type." );

			var msgs = md.Groups.SelectMany( g => g.Messages ).OrderBy( m => m.MessageIndex ).ToList();
			int count = msgs.Count;

			using var ms = new MemoryStream();
			using var bw = new BinaryWriter( ms, Encoding.ASCII, leaveOpen: true );

			bw.Write( 4u );
			long tablePos = ms.Position;
			bw.Write( (uint)count );
			long ptrsPos = ms.Position;
			for( int i = 0; i < count; i++ )
				bw.Write( 0u );

			var starts = new uint[ count ];
			for( int i = 0; i < count; i++ )
			{
				starts[ i ] = (uint)ms.Position;
				var msg = msgs[ i ];
				for( int pi = 0; pi < msg.Pages.Count; pi++ )
				{
					var bytes = msg.Pages[ pi ].Raw ?? Array.Empty<byte>();
					bw.Write( bytes );
					bw.Write( (byte)0xFF );
				}
			}

			long end = ms.Position;
			ms.Position = (int)ptrsPos;
			for( int i = 0; i < count; i++ )
				bw.Write( starts[ i ] );
			ms.Position = end;

			return ms.ToArray();
		}

		public IMsgCodec CreateCodec() => new FF1MsgCodec();

		public IMsgHighlighter? CreateHighlighter() => new FF1Highlighter();

		sealed class FF1Highlighter : IMsgHighlighter
		{
			static readonly Regex RxFD = new( @"\<FD(?<rgb>[0-9A-Fa-f]{6})\>", RegexOptions.Compiled );

			public IReadOnlyList<PaintSpan> GetSpans( string text )
			{
				var spans = new List<PaintSpan>();
				var matches = RxFD.Matches( text );
				for( int i = 0; i < matches.Count; i++ )
				{
					var m = matches[ i ];
					var rgb = m.Groups[ "rgb" ].Value;
					var color = Color.FromArgb(
						Convert.ToInt32( rgb.Substring( 0, 2 ), 16 ),
						Convert.ToInt32( rgb.Substring( 2, 2 ), 16 ),
						Convert.ToInt32( rgb.Substring( 4, 2 ), 16 ) );
					int spanStart = m.Index + m.Length;
					int spanEnd = ( i + 1 < matches.Count ) ? matches[ i + 1 ].Index : text.Length;
					if( spanEnd > spanStart )
					{
						spans.Add( new PaintSpan
						{
							Start = spanStart,
							Length = spanEnd - spanStart,
							ForeColor = color
						} );
					}
				}
				return spans;
			}

			public IReadOnlyList<HighlightRule> GetRules() => new[]
			{
				new HighlightRule(RxFD, fg: Color.DimGray),
			};
		}

		sealed class FF1MsgCodec : IMsgCodec
		{
			readonly Dictionary<byte, string> map;
			readonly Dictionary<string, byte> inv;

			public FF1MsgCodec()
			{
				map = BuildGlyphs();
				inv = map.Where( kv => !string.IsNullOrEmpty( kv.Value ) && kv.Key < 0xF0 )
						 .GroupBy( kv => kv.Value )
						 .ToDictionary( g => g.Key, g => g.First().Key, StringComparer.Ordinal );
			}

			public string DecodePage( byte[] pageBytes )
			{
				var sb = new StringBuilder();
				for( int i = 0; i < pageBytes.Length; i++ )
				{
					byte b = pageBytes[ i ];
					if( b == 0xFE )
					{ sb.Append( '\n' ); continue; }
					if( b == 0xFA )
					{ sb.Append( "<FA>\n" ); continue; }
					if( b is >= 0xF0 and <= 0xF3 )
					{
						if( i + 1 < pageBytes.Length )
							sb.Append( $"<B{( b - 0xEF )}:{pageBytes[ ++i ]:X2}>" );
						else
							sb.Append( $"<{b:X2}>" );
						continue;
					}
					if( b == 0xFB )
					{
						if( i + 2 < pageBytes.Length )
						{
							byte m = pageBytes[ ++i ];
							byte a = pageBytes[ ++i ];
							sb.Append( $"<FB:{m:X2},{a:X2}>" );
						}
						else
							sb.Append( "<FB>" );
						continue;
					}
					if( b == 0xFD )
					{
						if( i + 3 < pageBytes.Length )
						{
							byte r = pageBytes[ ++i ], g = pageBytes[ ++i ], bl = pageBytes[ ++i ];
							sb.Append( $"<FD{r:X2}{g:X2}{bl:X2}>" );
						}
						else
							sb.Append( "<FD>" );
						continue;
					}
					if( b == 0xF7 )
					{
						if( i + 1 < pageBytes.Length )
							sb.Append( $"<NX:{pageBytes[ ++i ]:X2}>" );
						else
							sb.Append( "<F7>" );
						continue;
					}
					if( b == 0xF8 )
					{
						if( i + 1 < pageBytes.Length )
							sb.Append( $"<NY:{pageBytes[ ++i ]:X2}>" );
						else
							sb.Append( "<F8>" );
						continue;
					}
					if( b == 0xF9 )
					{
						if( i + 3 < pageBytes.Length )
						{
							byte t = pageBytes[ ++i ];
							ushort x = (ushort)( pageBytes[ ++i ] | ( pageBytes[ ++i ] << 8 ) );
							sb.Append( $"<SEL:{t:X2},{x:X4}>" );
						}
						else
							sb.Append( "<F9>" );
						continue;
					}
					if( b is 0xF4 or 0xF5 or 0xF6 or 0xFC )
					{
						if( i + 2 < pageBytes.Length )
						{
							var a = pageBytes[ ++i ].ToString( "X2" ) + pageBytes[ ++i ].ToString( "X2" );
							sb.Append( $"<{b:X2}:{a}>" );
						}
						else
							sb.Append( $"<{b:X2}>" );
						continue;
					}
					if( map.TryGetValue( b, out var ch ) && !string.IsNullOrEmpty( ch ) )
						sb.Append( ch );
					else
						sb.Append( $"<{b:X2}>" );
				}
				return sb.ToString();
			}

			public byte[] EncodePage( string text )
			{
				var outBytes = new List<byte>();
				for( int i = 0; i < text.Length; i++ )
				{
					char c = text[ i ];
					if( c == '\n' )
					{ outBytes.Add( 0xFE ); continue; }
					if( c == '<' )
					{
						int close = text.IndexOf( '>', i + 1 );
						if( close > i && close - i <= 64 )
						{
							string tag = text.Substring( i + 1, close - i - 1 );
							if( string.Equals( tag, "FA", StringComparison.OrdinalIgnoreCase ) )
							{ outBytes.Add( 0xFA ); i = close; continue; }
							if( tag.StartsWith( "FD", StringComparison.OrdinalIgnoreCase ) && tag.Length == 8 )
							{
								if( byte.TryParse( tag.Substring( 2, 2 ), NumberStyles.HexNumber, null, out var r ) &&
									byte.TryParse( tag.Substring( 4, 2 ), NumberStyles.HexNumber, null, out var g ) &&
									byte.TryParse( tag.Substring( 6, 2 ), NumberStyles.HexNumber, null, out var bl ) )
								{ outBytes.Add( 0xFD ); outBytes.Add( r ); outBytes.Add( g ); outBytes.Add( bl ); i = close; continue; }
							}
							if( tag.StartsWith( "NX:", StringComparison.OrdinalIgnoreCase ) && tag.Length == 5 )
							{
								if( byte.TryParse( tag.Substring( 3, 2 ), NumberStyles.HexNumber, null, out var nx ) )
								{ outBytes.Add( 0xF7 ); outBytes.Add( nx ); i = close; continue; }
							}
							if( tag.StartsWith( "NY:", StringComparison.OrdinalIgnoreCase ) && tag.Length == 5 )
							{
								if( byte.TryParse( tag.Substring( 3, 2 ), NumberStyles.HexNumber, null, out var ny ) )
								{ outBytes.Add( 0xF8 ); outBytes.Add( ny ); i = close; continue; }
							}
							if( tag.StartsWith( "SEL:", StringComparison.OrdinalIgnoreCase ) )
							{
								var parts = tag.Substring( 4 ).Split( ',' );
								if( parts.Length == 2 &&
									byte.TryParse( parts[ 0 ], NumberStyles.HexNumber, null, out var t ) &&
									ushort.TryParse( parts[ 1 ], NumberStyles.HexNumber, null, out var x ) )
								{ outBytes.Add( 0xF9 ); outBytes.Add( t ); outBytes.Add( (byte)( x & 0xFF ) ); outBytes.Add( (byte)( x >> 8 ) ); i = close; continue; }
							}
							if( tag.StartsWith( "FB:", StringComparison.OrdinalIgnoreCase ) )
							{
								var body = tag.Substring( 3 );
								if( body.Contains( "," ) )
								{
									var parts = body.Split( ',' );
									if( parts.Length == 2 &&
										byte.TryParse( parts[ 0 ], NumberStyles.HexNumber, null, out var m ) &&
										byte.TryParse( parts[ 1 ], NumberStyles.HexNumber, null, out var a ) )
									{ outBytes.Add( 0xFB ); outBytes.Add( m ); outBytes.Add( a ); i = close; continue; }
								}
								else if( byte.TryParse( body, NumberStyles.HexNumber, null, out var m2 ) )
								{ outBytes.Add( 0xFB ); outBytes.Add( m2 ); outBytes.Add( 0x00 ); i = close; continue; }
							}
							if( tag.Length >= 2 && byte.TryParse( tag.Substring( 0, 2 ), NumberStyles.HexNumber, null, out var op ) )
							{
								if( op is >= 0xF0 and <= 0xF3 && tag.Length == 5 && tag[ 2 ] == ':' )
								{
									if( byte.TryParse( tag.Substring( 3, 2 ), NumberStyles.HexNumber, null, out var idx ) )
									{ outBytes.Add( op ); outBytes.Add( idx ); i = close; continue; }
								}
								if( op is 0xF4 or 0xF5 or 0xF6 or 0xFC )
								{
									if( tag.Length > 3 && tag[ 2 ] == ':' )
									{
										var hex = tag.Substring( 3 );
										if( hex.Length >= 4 )
										{ outBytes.Add( op ); outBytes.Add( Convert.ToByte( hex.Substring( 0, 2 ), 16 ) ); outBytes.Add( Convert.ToByte( hex.Substring( 2, 2 ), 16 ) ); i = close; continue; }
									}
								}
								if( tag.Length == 2 )
								{ outBytes.Add( op ); i = close; continue; }
							}
						}
					}
					var s = c.ToString();
					if( inv.TryGetValue( s, out var bb ) )
						outBytes.Add( bb );
					else
						outBytes.Add( (byte)'?' );
				}
				return outBytes.ToArray();
			}

			static Dictionary<byte, string> BuildGlyphs()
			{
				var d = new Dictionary<byte, string>();
				d[ 0x00 ] = " ";
				for( byte b = 0x01; b <= 0x1A; b++ )
					d[ b ] = ( (char)( 'A' + ( b - 1 ) ) ).ToString();
				for( byte b = 0x1B; b <= 0x34; b++ )
					d[ b ] = ( (char)( 'a' + ( b - 0x1B ) ) ).ToString();
				d[ 0x3F ] = "0";
				d[ 0x40 ] = "1";
				d[ 0x41 ] = "2";
				d[ 0x42 ] = "3";
				d[ 0x43 ] = "4";
				d[ 0x44 ] = "5";
				d[ 0x45 ] = "6";
				d[ 0x46 ] = "7";
				d[ 0x47 ] = "8";
				d[ 0x48 ] = "9";
				d[ 0x8A ] = "\"";
				d[ 0x8B ] = "'";
				d[ 0x8C ] = "(";
				d[ 0x8D ] = ")";
				d[ 0x8E ] = "-";
				d[ 0x8F ] = "?";
				d[ 0x90 ] = "/";
				d[ 0x91 ] = "’";
				d[ 0x92 ] = "、";
				d[ 0x93 ] = ";";
				d[ 0x94 ] = ":";
				d[ 0x95 ] = ",";
				d[ 0x96 ] = ".";
				d[ 0x97 ] = "!";
				d[ 0xA6 ] = "<circle>";
				d[ 0xA7 ] = "<cross>";
				d[ 0xA8 ] = "<triangle>";
				d[ 0xA9 ] = "<square>";
				return d;
			}
		}
	}
}
