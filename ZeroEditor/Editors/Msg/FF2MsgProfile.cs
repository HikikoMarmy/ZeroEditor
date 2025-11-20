using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroEditor.Editors
{
	sealed class FF2MsgProfile : IMsgProfile
	{
		public string Name => "FF2";
		public int FixedGroupTableOffset => 0x00;
		const int TypeMax = 0x53;

		static readonly int[] MsgTypeMax = new int[]
		{
			0x0E,0x12C,0x12C,0x0B,0x01,0x01,0x130,0x0A,0x04,0x15B,0x1D,0x08,0x0E,0x3D,0x1F,0x1F,
			0x1F,0x33,0x1F,0x1F,0x1F,0x1F,0x1F,0x1F,0x6A,0x11,0x06,0x1E,0x11,0x78,0x7E,0x4E,
			0x7E,0x169,0x0A,0x06,0x12,0x96,0x52,0x64,0x06,0x32,0x2C,0x3A,0x3A,0x01,0x05,0x01,
			0x78,0x58,0x01,0x0B,0x01,0x01,0x05,0x01,0x02,0x01,0x08,0x21F,0x5C,0x11,0x01,0x01,
			0x01,0x38,0x06,0x07,0x07,0x0A,0x02,0x09,0x28,0x28,0xF0,0x07,0x09,0x22E,0x1F,0x01,
			0x3A,0x11,0x3A
		};
		

		static bool TryReadTypeHeader( byte[] blob, int offset, out int[] typeOffsets )
		{
			typeOffsets = Array.Empty<int>();
			if( blob == null || offset < 0 || offset + TypeMax * 4 > blob.Length )
				return false;
			var tbl = new int[ TypeMax ];
			Buffer.BlockCopy( blob, offset, tbl, 0, TypeMax * 4 );
			typeOffsets = tbl;
			return true;
		}

		public IMsgDocument Parse( byte[] blob )
		{
			var doc = new MsgDocument();
			if( !TryReadTypeHeader( blob, FixedGroupTableOffset, out var typeOffs ) )
				return doc;

			var perTypePtrs = new Dictionary<int, List<int>>();
			var globalPtrs = new SortedSet<int>();

			for( int t = 0; t < TypeMax; t++ )
			{
				int baseOff = typeOffs[ t ];
				int count = ( t >= 0 && t < MsgTypeMax.Length ) ? MsgTypeMax[ t ] : 0;
				var list = new List<int>( Math.Max( 0, count ) );
				if( baseOff > 0 && baseOff + count * 4 <= blob.Length )
				{
					for( int i = 0; i < count; i++ )
					{
						int p = BitConverter.ToInt32( blob, baseOff + i * 4 );
						if( p > 0 && p < blob.Length )
						{
							list.Add( p );
							globalPtrs.Add( p );
						}
					}
				}
				perTypePtrs[ t ] = list.OrderBy( v => v ).ToList();
			}

			var orderedGlobal = globalPtrs.ToList();
			var nextCap = new Dictionary<int, int>( orderedGlobal.Count );
			for( int i = 0; i < orderedGlobal.Count; i++ )
			{
				int s = orderedGlobal[ i ];
				int e = ( i + 1 < orderedGlobal.Count ) ? orderedGlobal[ i + 1 ] : blob.Length;
				nextCap[ s ] = e;
			}

			var codec = CreateCodec();

			for( int t = 0; t < TypeMax; t++ )
			{
				var grp = new MsgGroup { GroupIndex = t };
				var list = perTypePtrs[ t ];
				for( int id = 0; id < list.Count; id++ )
				{
					int start = list[ id ];
					if( !nextCap.TryGetValue( start, out int cap ) )
						cap = blob.Length;
					if( cap <= start )
						continue;

					var msg = new MsgMessage { GroupIndex = t, MessageIndex = id };
					int cur = start, i = start;
					bool ended = false;
					while( i < cap )
					{
						byte b = blob[ i ];
						if( b == 0xFB )
						{
							int len = i - cur;
							if( len > 0 && cur + len <= blob.Length )
							{
								var pageBytes = new byte[ len ];
								Buffer.BlockCopy( blob, cur, pageBytes, 0, len );
								msg.Pages.Add( new MsgPage { GroupIndex = t, MessageIndex = id, PageIndex = msg.Pages.Count, Raw = pageBytes, Text = codec.DecodePage( pageBytes ) } );
							}
							cur = i + 1;
							i++;
							continue;
						}
						if( b == 0xFF )
						{
							int len = i - cur;
							if( len > 0 && cur + len <= blob.Length )
							{
								var pageBytes = new byte[ len ];
								Buffer.BlockCopy( blob, cur, pageBytes, 0, len );
								msg.Pages.Add( new MsgPage { GroupIndex = t, MessageIndex = id, PageIndex = msg.Pages.Count, Raw = pageBytes, Text = codec.DecodePage( pageBytes ) } );
							}
							ended = true;
							break;
						}
						i++;
					}
					if( !ended )
					{
						int len = Math.Max( 0, Math.Min( cap, blob.Length ) - cur );
						if( len > 0 )
						{
							var pageBytes = new byte[ len ];
							Buffer.BlockCopy( blob, cur, pageBytes, 0, len );
							msg.Pages.Add( new MsgPage { GroupIndex = t, MessageIndex = id, PageIndex = msg.Pages.Count, Raw = pageBytes, Text = codec.DecodePage( pageBytes ) } );
						}
					}
					grp.Messages.Add( msg );
				}
				doc.MutableGroups.Add( grp );
			}

			return doc;
		}

		public byte[] Save( IMsgDocument doc )
		{
			if( doc is not MsgDocument md )
				throw new ArgumentException( "Unexpected doc type." );
			var groups = md.Groups?.ToList() ?? new List<MsgGroup>();
			if( groups.Count == 0 )
				groups.Add( new MsgGroup { GroupIndex = 0 } );

			var ms = new MemoryStream();
			using var bw = new BinaryWriter( ms, Encoding.ASCII, leaveOpen: true );

			var typeOffsets = new uint[ TypeMax ];
			long headerPos = ms.Position;
			for( int i = 0; i < TypeMax; i++ )
				bw.Write( 0u );

			var idTables = new Dictionary<int, (int count, long pos)>();
			for( int t = 0; t < TypeMax; t++ )
			{
				var grp = groups.FirstOrDefault( g => g.GroupIndex == t );
				int maxId = grp?.Messages.Count > 0 ? grp.Messages.Max( m => m.MessageIndex ) : -1;
				int limit = ( t >= 0 && t < MsgTypeMax.Length ) ? MsgTypeMax[ t ] : 0;
				int count = Math.Max( 0, Math.Min( maxId + 1, limit ) );
				if( count == 0 )
				{ typeOffsets[ t ] = 0; continue; }
				typeOffsets[ t ] = (uint)ms.Position;
				idTables[ t ] = (count, ms.Position);
				for( int i = 0; i < count; i++ )
					bw.Write( 0u );
			}

			var ptrsByType = new Dictionary<int, uint[]>();
			for( int t = 0; t < TypeMax; t++ )
			{
				if( !idTables.TryGetValue( t, out var info ) )
					continue;
				var grp = groups.First( g => g.GroupIndex == t );
				var byId = new Dictionary<int, MsgMessage>();
				foreach( var m in grp.Messages )
					if( m.MessageIndex < info.count )
						byId[ m.MessageIndex ] = m;
				var starts = new uint[ info.count ];
				for( int id = 0; id < info.count; id++ )
				{
					if( !byId.TryGetValue( id, out var msg ) )
						continue;
					starts[ id ] = (uint)ms.Position;
					for( int pi = 0; pi < msg.Pages.Count; pi++ )
					{
						var bytes = msg.Pages[ pi ].Raw ?? Array.Empty<byte>();
						bw.Write( bytes );
						if( pi < msg.Pages.Count - 1 )
							bw.Write( (byte)0xFB );
					}
					bw.Write( (byte)0xFF );
				}
				ptrsByType[ t ] = starts;
			}

			long endPos = ms.Position;
			ms.Position = headerPos;
			for( int i = 0; i < TypeMax; i++ )
				bw.Write( typeOffsets[ i ] );
			foreach( var kv in idTables )
			{
				int t = kv.Key;
				int count = kv.Value.count;
				long pos = kv.Value.pos;
				ms.Position = pos;
				var starts = ptrsByType.TryGetValue( t, out var arr ) ? arr : new uint[ count ];
				for( int i = 0; i < count; i++ )
					bw.Write( starts[ i ] );
			}
			ms.Position = endPos;
			return ms.ToArray();
		}

		public IMsgCodec CreateCodec() => new FF2MsgCodec();

		public IMsgHighlighter? CreateHighlighter() => new FF2Highlighter();

		sealed class FF2Highlighter : IMsgHighlighter
		{
			static readonly Regex RxFDNum = new Regex( "<FD:(?<hh>[0-9A-Fa-f]{2})>", RegexOptions.Compiled );
			static readonly Regex rxWaitInput = new Regex( "<WaitInput>", RegexOptions.Compiled );
			static readonly Regex rxRawByte = new Regex( "<[0-9A-Fa-f]{2}>", RegexOptions.Compiled );

			static readonly Dictionary<byte, Color> ColorMap = new()
			{
				{ 0x00, Color.FromArgb( 255, 255, 255 ) },
				{ 0x01, Color.FromArgb( 155,155,120 ) },
				{ 0x25, Color.FromArgb( 250,80,40 ) },
				{ 0x26, Color.LightSkyBlue },
				{ 0x27, Color.LimeGreen },
				{ 0x28, Color.PaleVioletRed },
				{ 0x29, Color.FromArgb( 180, 130, 50 ) },

				{ 0x2B, Color.FromArgb( 255, 130, 130 ) },
				{ 0x2C, Color.CadetBlue },
				{ 0x2D, Color.Peru },
			};

			public IReadOnlyList<PaintSpan> GetSpans( string text )
			{
				var spans = new List<PaintSpan>();
				var switches = new List<(int idx, byte id)>();

				foreach( Match m in RxFDNum.Matches( text ) )
				{
					if( byte.TryParse( m.Groups[ "hh" ].Value, NumberStyles.HexNumber, null, out var id ) )
						switches.Add( (m.Index + m.Length, id) );
				}

				switches.Sort( ( a, b ) => a.idx.CompareTo( b.idx ) );

				for( int i = 0; i < switches.Count; i++ )
				{
					int spanStart = switches[ i ].idx;
					int spanEnd = ( i + 1 < switches.Count ) ? switches[ i + 1 ].idx : text.Length;
					if( spanEnd <= spanStart )
						continue;

					var id = switches[ i ].id;
					if( ColorMap.TryGetValue( id, out var color ) )
					{
						spans.Add( new PaintSpan { Start = spanStart, Length = spanEnd - spanStart, ForeColor = color } );
					}
				}

				return spans;
			}

			public IReadOnlyList<HighlightRule> GetRules() => new[]
			{
				new HighlightRule(RxFDNum, fg: Color.DimGray),
				new HighlightRule(rxWaitInput, fg: Color.SeaGreen, bold: true),
				new HighlightRule(rxRawByte, fg: Color.DimGray),
			};
		}

		sealed class FF2MsgCodec : IMsgCodec
		{
			readonly Dictionary<byte, string> map;
			readonly Dictionary<string, byte> inv;

			public FF2MsgCodec()
			{
				map = BuildGlyphs();
				inv = map.Where( kv => !string.IsNullOrEmpty( kv.Value ) && kv.Key < 0xF0 ).GroupBy( kv => kv.Value ).ToDictionary( g => g.Key, g => g.First().Key, StringComparer.Ordinal );
			}

			public string DecodePage( byte[] pageBytes )
			{
				var sb = new StringBuilder();
				int i = 0;
				while( i < pageBytes.Length )
				{
					byte b = pageBytes[ i++ ];
					if( b >= 0xF0 && b <= 0xF3 )
					{
						byte bank = i < pageBytes.Length ? pageBytes[ i++ ] : (byte)0;
						byte ch = i < pageBytes.Length ? pageBytes[ i++ ] : (byte)0;
						if( map.TryGetValue( ch, out var s ) && !string.IsNullOrEmpty( s ) )
							sb.Append( s );
						else
							sb.Append( $"<{ch:X2}>" );
						continue;
					}
					if( b == 0xF7 )
					{ i = Math.Min( pageBytes.Length, i + 4 ); continue; }
					if( b == 0xF8 )
					{ i = Math.Min( pageBytes.Length, i + 8 ); continue; }
					if( b == 0xF9 )
					{ byte p = i < pageBytes.Length ? pageBytes[ i++ ] : (byte)0; sb.Append( $"<F9:{p:X2}>" ); continue; }
					if( b == 0xFB )
					{ sb.Append( "<PB>" ); continue; }
					if( b == 0xFC )
					{ byte n = i < pageBytes.Length ? pageBytes[ i++ ] : (byte)0; sb.Append( $"<NUM:{n:X2}>" ); continue; }
					if( b == 0xFD )
					{ byte p = i < pageBytes.Length ? pageBytes[ i++ ] : (byte)0; sb.Append( $"<FD:{p:X2}>" ); continue; }
					if( b == 0xFE )
					{ sb.Append( '\n' ); continue; }
					if( b == 0xFF )
						break;
					if( map.TryGetValue( b, out var chs ) && !string.IsNullOrEmpty( chs ) )
						sb.Append( chs );
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
							if( string.Equals( tag, "PB", StringComparison.OrdinalIgnoreCase ) )
							{ outBytes.Add( 0xFB ); i = close; continue; }
							if( tag.StartsWith( "NUM:", StringComparison.OrdinalIgnoreCase ) )
							{
								var hex = tag.Substring( 4 );
								if( hex.Length >= 2 && byte.TryParse( hex, NumberStyles.HexNumber, null, out var v ) )
								{ outBytes.Add( 0xFC ); outBytes.Add( v ); i = close; continue; }
							}
							if( tag.StartsWith( "FD:", StringComparison.OrdinalIgnoreCase ) )
							{
								var hex = tag.Substring( 3 );
								if( hex.Length >= 2 && byte.TryParse( hex, NumberStyles.HexNumber, null, out var v ) )
								{ outBytes.Add( 0xFD ); outBytes.Add( v ); i = close; continue; }
							}
							if( tag.Length == 2 && byte.TryParse( tag, NumberStyles.HexNumber, null, out var op ) )
							{ outBytes.Add( op ); i = close; continue; }
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
				for( int i = 0; i < 10; i++ )
				{ d[ (byte)( 0x35 + i ) ] = i.ToString(); d[ (byte)( 0x3F + i ) ] = i.ToString(); }
				d[ 0x49 ] = "<circle>";
				d[ 0x4A ] = "<cross>";
				d[ 0x4B ] = "<triangle>";
				d[ 0x4C ] = "<square>";
				d[ 0x4D ] = "(";
				d[ 0x4E ] = ")";
				d[ 0x4F ] = ",";
				d[ 0x50 ] = "?";
				d[ 0x51 ] = "!";
				d[ 0x52 ] = "/";
				d[ 0x53 ] = "•";
				d[ 0x54 ] = ":";
				d[ 0x55 ] = "✱";
				d[ 0x56 ] = "~";
				d[ 0x57 ] = "-";
				d[ 0x58 ] = "'";
				d[ 0x59 ] = ".";
				d[ 0x5A ] = "  ";
				d[ 0x5B ] = "”";
				d[ 0x6D ] = "&";
				d[ 0x6E ] = "Ⅱ";
				d[ 0x6F ] = "“";
				d[ 0x70 ] = "⁉";
				d[ 0xB0 ] = "®";
				return d;
			}
		}
	}
}
