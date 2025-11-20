using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Linq;

namespace ZeroEditor.Editors
{
	sealed class FF3MsgProfile : IMsgProfile
	{
		public string Name => "FF3";
		public int FixedGroupTableOffset => 0x6C;

		public bool TryReadGroupTable( byte[] blob, int offset, out int[] groupStarts )
		{
			groupStarts = Array.Empty<int>();
			if( offset < 0 || offset + 36 > blob.Length )
				return false;
			var s = new int[ 9 ];
			for( int i = 0; i < 9; i++ )
				s[ i ] = BitConverter.ToInt32( blob, offset + i * 4 );
			int prev = -1;
			for( int i = 0; i < 9; i++ )
			{ int v = s[ i ]; if( v <= prev || v < 0 || v > blob.Length ) return false; prev = v; }
			groupStarts = s;
			return true;
		}

		public IMsgDocument Parse( byte[] blob )
		{
			var doc = new MsgDocument();
			if( blob == null || blob.Length < 0x6C + 36 )
				return doc;

			doc.FF3_OrigLen = blob.Length;
			doc.FF3_HeaderPrefix = blob.Take( 0x6C ).ToArray();
			doc.FF3_HeaderU32 = new uint[ 27 ];
			for( int i = 0; i < 27; i++ )
				doc.FF3_HeaderU32[ i ] = BitConverter.ToUInt32( blob, i * 4 );

			var s = new int[ 9 ];
			for( int i = 0; i < 9; i++ )
				s[ i ] = BitConverter.ToInt32( blob, 0x6C + i * 4 );
			int prev = -1;
			for( int i = 0; i < 9; i++ )
			{ int v = s[ i ]; if( v <= prev || v < 0 || v > blob.Length ) return doc; prev = v; }
			doc.FF3_OrigS = s;

			var diag = FF3MsgHeuristics.Analyze( blob, s );
			var codec = CreateCodec();

			var globalPtrs = new SortedSet<int>();
			var perGroupPtrs = new Dictionary<int, List<int>>();
			for( int gi = 0; gi < 8; gi++ )
			{
				int gstart = s[ gi ];
				int gend = s[ gi + 1 ];
				int cnt = diag.InferredCounts.TryGetValue( gi, out var c ) ? c : 0;
				var list = new List<int>( Math.Max( 0, cnt ) );
				if( gstart > 0 && gend > gstart && gend <= blob.Length )
				{
					for( int k = 0; k < cnt; k++ )
					{
						int ptr = BitConverter.ToInt32( blob, gstart + 4 * k );
						if( ptr > 0 && ptr < blob.Length )
						{ list.Add( ptr ); globalPtrs.Add( ptr ); }
					}
				}
				perGroupPtrs[ gi ] = list.OrderBy( v => v ).ToList();
			}

			var orderedGlobal = globalPtrs.ToList();
			var nextCap = new Dictionary<int, int>( orderedGlobal.Count );
			for( int i = 0; i < orderedGlobal.Count; i++ )
			{
				int st = orderedGlobal[ i ];
				int en = ( i + 1 < orderedGlobal.Count ) ? orderedGlobal[ i + 1 ] : blob.Length;
				nextCap[ st ] = en;
			}

			for( int gi = 0; gi < 8; gi++ )
			{
				var grp = new MsgGroup { GroupIndex = gi };
				var starts = perGroupPtrs[ gi ];
				for( int mi = 0; mi < starts.Count; mi++ )
				{
					int start = starts[ mi ];
					if( !nextCap.TryGetValue( start, out int cap ) )
						cap = blob.Length;
					if( cap <= start )
						continue;

					var msg = new MsgMessage { GroupIndex = gi, MessageIndex = mi };

					int cur = start;
					for( int i = start; i < cap; i++ )
					{
						byte b = blob[ i ];
						if( b == 0xFF )
						{
							int len = i - cur;
							if( len > 0 )
							{
								var pageBytes = new byte[ len ];
								Buffer.BlockCopy( blob, cur, pageBytes, 0, len );
								int prel = ComputePreludeLength( pageBytes );
								var body = new byte[ Math.Max( 0, pageBytes.Length - prel ) ];
								if( body.Length > 0 )
									Buffer.BlockCopy( pageBytes, prel, body, 0, body.Length );
								msg.Pages.Add( new MsgPage { GroupIndex = gi, MessageIndex = mi, PageIndex = msg.Pages.Count, PreludeRaw = prel > 0 ? pageBytes.Take( prel ).ToArray() : Array.Empty<byte>(), Raw = pageBytes, Text = codec.DecodePage( body ) } );
							}
							cur = i + 1;
						}
					}
					if( cur < cap )
					{
						var pageBytes = new byte[ cap - cur ];
						Buffer.BlockCopy( blob, cur, pageBytes, 0, pageBytes.Length );
						int prel = ComputePreludeLength( pageBytes );
						var body = new byte[ Math.Max( 0, pageBytes.Length - prel ) ];
						if( body.Length > 0 )
							Buffer.BlockCopy( pageBytes, prel, body, 0, body.Length );
						msg.Pages.Add( new MsgPage { GroupIndex = gi, MessageIndex = mi, PageIndex = msg.Pages.Count, PreludeRaw = prel > 0 ? pageBytes.Take( prel ).ToArray() : Array.Empty<byte>(), Raw = pageBytes, Text = codec.DecodePage( body ) } );
					}

					grp.Messages.Add( msg );
				}
				doc.MutableGroups.Add( grp );
			}

			return doc;
		}

		static bool IsPrintable( byte b )
		{
			if( b == 0x00 )
				return true;
			if( b >= 0x01 && b <= 0x34 )
				return true;
			if( b >= 0x35 && b <= 0x3E )
				return true;
			if( b >= 0x3F && b <= 0x48 )
				return true;
			if( b >= 0x49 && b <= 0x5B )
				return true;
			if( b == 0x6F || b == 0xB0 )
				return true;
			return false;
		}

		static bool IsControl( byte b )
		{
			if( b == 0xFE || b == 0xFB || b == 0xFC || b == 0xFD )
				return true;
			if( b >= 0xEF && b <= 0xF3 )
				return true;
			return false;
		}

		static int ComputePreludeLength( byte[] pageBytes )
		{
			if( pageBytes == null || pageBytes.Length == 0 )
				return 0;
			int w = 16;
			int threshold = 10;
			for( int i = 0; i < pageBytes.Length; i++ )
			{
				int n = Math.Min( w, pageBytes.Length - i );
				int printable = 0;
				for( int k = 0; k < n; k++ )
				{
					byte b = pageBytes[ i + k ];
					if( IsPrintable( b ) || IsControl( b ) )
						printable++;
				}
				if( printable >= threshold )
					return i;
			}
			return 0;
		}

		public byte[] Save( IMsgDocument doc )
		{
			if( doc is not MsgDocument md )
				throw new ArgumentException( "Unexpected doc type." );

			int groupsToWrite = Math.Min( md.Groups.Count, 8 );
			int[] sectionSizes = new int[ 8 ];

			for( int gi = 0; gi < 8; gi++ )
			{
				if( gi >= groupsToWrite )
				{ sectionSizes[ gi ] = 0; continue; }
				var msgs = md.MutableGroups[ gi ].Messages;
				int msgCount = msgs.Count;
				int pointerTableBytes = 4 * msgCount;
				int bodiesBytes = 0;
				foreach( var m in msgs )
				{
					if( m.Pages.Count == 0 )
					{ bodiesBytes += 1; continue; }
					foreach( var p in m.Pages )
					{
						var bytes = p.Raw ?? Array.Empty<byte>();
						bodiesBytes += bytes.Length + 1;
					}
				}
				sectionSizes[ gi ] = pointerTableBytes + bodiesBytes;
			}

			int[] newS = new int[ 9 ];
			newS[ 0 ] = 0x6C + 36;
			for( int i = 1; i < 9; i++ )
				newS[ i ] = newS[ i - 1 ] + ( i - 1 < 8 ? sectionSizes[ i - 1 ] : 0 );

			uint[] relocatedHeaderDW;
			if( md.FF3_HeaderU32 != null && md.FF3_OrigS != null && md.FF3_OrigS.Length == 9 && md.FF3_OrigLen > 0 )
			{
				relocatedHeaderDW = RelocateHeader( md.FF3_HeaderU32, md.FF3_OrigS, md.FF3_OrigLen, newS );
			}
			else
			{
				relocatedHeaderDW = new uint[ 27 ];
			}

			var ms = new MemoryStream();
			using var bw = new BinaryWriter( ms, Encoding.ASCII, leaveOpen: true );

			for( int i = 0; i < 27; i++ )
				bw.Write( relocatedHeaderDW[ i ] );
			for( int i = 0; i < 9; i++ )
				bw.Write( newS[ i ] );

			for( int gi = 0; gi < 8; gi++ )
			{
				var msgs = gi < groupsToWrite ? md.MutableGroups[ gi ].Messages : null;
				int msgCount = msgs?.Count ?? 0;

				long ptrTablePos = ms.Position;
				for( int i = 0; i < msgCount; i++ )
					bw.Write( 0 );

				var absStarts = new List<int>( msgCount );

				if( msgs != null )
				{
					for( int mi = 0; mi < msgs.Count; mi++ )
					{
						absStarts.Add( (int)ms.Position );
						var pages = msgs[ mi ].Pages;
						if( pages.Count == 0 )
						{
							bw.Write( (byte)0xFF );
						}
						else
						{
							foreach( var p in pages )
							{
								var bytes = p.Raw ?? Array.Empty<byte>();
								bw.Write( bytes );
								bw.Write( (byte)0xFF );
							}
						}
					}
				}

				long endPos = ms.Position;
				ms.Position = ptrTablePos;
				foreach( var sabs in absStarts )
					bw.Write( sabs );
				ms.Position = endPos;
			}

			return ms.ToArray();
		}

		private static uint[] RelocateHeader( uint[] origHeaderDW, int[] origS, int origLen, int[] newS )
		{
			var relocated = new uint[ origHeaderDW.Length ];
			for( int k = 0; k < origHeaderDW.Length; k++ )
			{
				uint H = origHeaderDW[ k ];
				bool done = false;
				for( int i = 0; i < 8; i++ )
				{
					if( H >= origS[ i ] && H < origS[ i + 1 ] )
					{
						int delta = newS[ i ] - origS[ i ];
						relocated[ k ] = (uint)( H + delta );
						done = true;
						break;
					}
				}
				if( !done && H >= origS[ 8 ] && H < (uint)origLen )
				{
					int deltaTail = newS[ 8 ] - origS[ 8 ];
					relocated[ k ] = (uint)( H + deltaTail );
					done = true;
				}
				if( !done )
					relocated[ k ] = H;
			}
			return relocated;
		}

		public IMsgCodec CreateCodec() => new FF3MsgCodec();
		public IMsgHighlighter? CreateHighlighter() => new FF3Highlighter();

		sealed class FF3Highlighter : IMsgHighlighter
		{
			static readonly Regex RxFDNum = new Regex( "<FD:(?<hh>[0-9A-Fa-f]{2})>", RegexOptions.Compiled );
			static readonly Regex rxWaitInput = new Regex( "<WaitInput>", RegexOptions.Compiled );
			static readonly Regex rxRawByte = new Regex( "<[0-9A-Fa-f]{2}>", RegexOptions.Compiled );

			static readonly Dictionary<byte, Color> ColorMap = new()
			{
				{ 0x25, Color.Firebrick },
				{ 0x26, Color.SteelBlue },
				{ 0x27, Color.LimeGreen },
				{ 0x28, Color.PaleVioletRed },
				{ 0x29, Color.DarkOrange },
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

		sealed class FF3MsgCodec : IMsgCodec
		{
			readonly Dictionary<byte, string> map;
			readonly Dictionary<string, byte> inv;

			public FF3MsgCodec()
			{
				map = BuildGlyphs();
				inv = new Dictionary<string, byte>( StringComparer.Ordinal );
				foreach( var kv in map )
					if( !string.IsNullOrEmpty( kv.Value ) && kv.Key < 0xF0 && !inv.ContainsKey( kv.Value ) )
						inv[ kv.Value ] = kv.Key;
			}

			public string DecodePage( byte[] pageBytes )
			{
				var sb = new StringBuilder();
				for( int i = 0; i < pageBytes.Length; i++ )
				{
					byte b = pageBytes[ i ];
					if( b == 0xFE )
					{ sb.Append( '\n' ); continue; }
					if( b == 0xFB )
					{ sb.Append( "\n<WaitInput>\n" ); continue; }
					if( b >= 0xEF && b <= 0xF3 )
					{ continue; }
					if( b == 0xFC )
					{ sb.Append( "%d" ); continue; }
					if( b == 0xFD )
					{
						if( i + 1 < pageBytes.Length )
						{ byte id = pageBytes[ ++i ]; sb.Append( $"<FD:{id:X2}>" ); }
						else
						{ sb.Append( "<FD>" ); }
						continue;
					}
					if( map.TryGetValue( b, out var ch2 ) && !string.IsNullOrEmpty( ch2 ) )
						sb.Append( ch2 );
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
					if( c == '%' && i + 1 < text.Length && text[ i + 1 ] == 'd' )
					{ outBytes.Add( 0xFC ); i += 1; continue; }
					if( c == '<' )
					{
						int close = text.IndexOf( '>', i + 1 );
						if( close > i && close - i <= 64 )
						{
							string tag = text.Substring( i + 1, close - i - 1 );
							if( tag.StartsWith( "FD:", StringComparison.OrdinalIgnoreCase ) && tag.Length == 5 )
							{
								if( byte.TryParse( tag.Substring( 3, 2 ), NumberStyles.HexNumber, null, out var id ) )
								{ outBytes.Add( 0xFD ); outBytes.Add( id ); i = close; continue; }
							}
							if( string.Equals( tag, "bullet", StringComparison.OrdinalIgnoreCase ) )
							{ outBytes.Add( 0x53 ); i = close; continue; }
							if( string.Equals( tag, "circle", StringComparison.OrdinalIgnoreCase ) )
							{ outBytes.Add( 0x49 ); i = close; continue; }
							if( string.Equals( tag, "cross", StringComparison.OrdinalIgnoreCase ) )
							{ outBytes.Add( 0x4A ); i = close; continue; }
							if( string.Equals( tag, "triangle", StringComparison.OrdinalIgnoreCase ) )
							{ outBytes.Add( 0x4B ); i = close; continue; }
							if( string.Equals( tag, "square", StringComparison.OrdinalIgnoreCase ) )
							{ outBytes.Add( 0x4C ); i = close; continue; }
							if( tag.Length == 2 && byte.TryParse( tag, NumberStyles.HexNumber, null, out var op ) )
							{ outBytes.Add( op ); i = close; continue; }
						}
					}
					if( inv.TryGetValue( c.ToString(), out var bb2 ) )
						outBytes.Add( bb2 );
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
				d[ 0x55 ] = "≠";
				d[ 0x56 ] = "~";
				d[ 0x57 ] = "-";
				d[ 0x58 ] = "'";
				d[ 0x59 ] = ".";
				d[ 0x5A ] = "  ";
				d[ 0x5B ] = "”";
				d[ 0x6F ] = "“";
				d[ 0xB0 ] = "®";
				return d;
			}
		}
	}

	public sealed class FF3DiagResult
	{
		public Dictionary<int, int> InferredCounts { get; } = new Dictionary<int, int>();
		public Dictionary<int, int> FirstBadIndex { get; } = new Dictionary<int, int>();
	}

	static class FF3MsgHeuristics
	{
		static int MeasureMsgLenStrict( byte[] b, int start, int hardCap )
		{
			if( start < 0 || start >= b.Length )
				return -1;
			int i = start;
			int cap = Math.Min( hardCap, b.Length );
			while( i < cap )
			{
				byte op = b[ i++ ];
				if( op >= 0xF0 && op <= 0xF3 )
				{ if( i + 2 > cap ) return -1; i += 2; continue; }
				if( op == 0xF7 )
				{ if( i + 4 > cap ) return -1; i += 4; continue; }
				if( op == 0xF8 )
				{ if( i + 8 > cap ) return -1; i += 8; continue; }
				if( op == 0xF9 || op == 0xFD || op == 0xFC )
				{ if( i + 1 > cap ) return -1; i += 1; continue; }
				if( op == 0xFE )
				{ continue; }
				if( op == 0xFF )
					return i - start;
			}
			return -1;
		}

		static bool LooksLikePtrRun( byte[] b, int off, int maxWords, int blobLen )
		{
			if( off < 0 || off + 4 > b.Length )
				return false;
			int prev = -1;
			int ok = 0;
			int words = Math.Min( maxWords, ( b.Length - off ) / 4 );
			for( int i = 0; i < words; i++ )
			{
				int p = BitConverter.ToInt32( b, off + i * 4 );
				if( p <= 0 || p >= blobLen )
					break;
				if( prev >= 0 && p <= prev )
					break;
				prev = p;
				ok++;
			}
			return ok >= Math.Max( 4, Math.Min( 12, maxWords / 2 ) );
		}

		public static FF3DiagResult Analyze( byte[] blob, int[] s )
		{
			var res = new FF3DiagResult();
			if( blob == null || s == null || s.Length != 9 )
				return res;

			for( int gi = 0; gi < 8; gi++ )
			{
				int baseOff = s[ gi ];
				int boundary = s[ gi + 1 ];
				if( baseOff <= 0 || boundary <= baseOff || boundary > blob.Length )
				{ res.InferredCounts[ gi ] = 0; continue; }

				int maxSlots = Math.Max( 0, ( boundary - baseOff ) / 4 );
				int inferred = 0;
				int lastPtr = -1;
				int lastLen = 0;

				for( int i = 0; i < maxSlots; i++ )
				{
					int p = BitConverter.ToInt32( blob, baseOff + i * 4 );
					if( p <= 0 || p >= blob.Length )
					{ res.FirstBadIndex[ gi ] = i; break; }

					if( lastPtr >= 0 )
					{
						int maxAllowed = lastPtr + lastLen + 1;
						if( p <= lastPtr || p > maxAllowed )
						{ res.FirstBadIndex[ gi ] = i; break; }
					}

					if( LooksLikePtrRun( blob, p, 24, blob.Length ) && lastPtr >= 0 && p >= lastPtr + lastLen )
					{
						res.FirstBadIndex[ gi ] = i;
						break;
					}

					int len = MeasureMsgLenStrict( blob, p, blob.Length );
					if( len < 0 )
					{ res.FirstBadIndex[ gi ] = i; break; }

					inferred++;
					lastPtr = p;
					lastLen = len;
				}

				res.InferredCounts[ gi ] = inferred;
				if( !res.FirstBadIndex.ContainsKey( gi ) )
					res.FirstBadIndex[ gi ] = inferred;
			}

			return res;
		}
	}
}
