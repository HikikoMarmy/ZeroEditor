using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroEditor.Archive
{
	public static class FileTyper
	{
		private const uint MAGIC_PK4 = 0x00344B50;
		private const uint MAGIC_PHF = 0x00666870;
		private const uint MAGIC_PZB = 0x00627A70;
		private const uint MAGIC_TIM2 = 0x324D4954;
		private const uint MAGIC_CLT2 = 0x32544C43;
		private const uint MAGIC_SGD = 0x00001050;

		private static readonly HashSet<string> KnownTokens = new( StringComparer.OrdinalIgnoreCase )
		{
			"ZLD","PZB","B2D","RMD","BMD","TPK","PSS","TM2","CL2"
		};

		public static string GuessFromHeader( byte[] header )
		{
			if( header == null || header.Length == 0 )
				return ".bin";

			if( header.Length >= 4 )
			{
				uint sig = BitConverter.ToUInt32( header, 0 );
				if( sig == MAGIC_PK4 )
					return ".pk4";
				if( sig == MAGIC_PHF )
					return ".phf";
				if( sig == MAGIC_PZB )
					return ".pzb";
				if( sig == MAGIC_TIM2 )
					return ".tm2";
				if( sig == MAGIC_CLT2 )
					return ".cl2";
				if( sig == MAGIC_SGD )
					return ".sgd";
			}

			int n = Math.Min( header.Length, 32 );
			int i = 0, run = 0, start = 0;
			while( i < n && run < 4 )
			{
				byte c = header[ i ];
				bool ok = ( c >= 'A' && c <= 'Z' ) || ( c >= 'a' && c <= 'z' ) || ( c >= '0' && c <= '9' ) || c == '_';
				if( !ok )
					break;
				run++;
				i++;
			}

			if( run >= 2 )
			{
				int len = run;
				while( len > 0 && ( header[ start + len - 1 ] == (byte)'_' || header[ start + len - 1 ] == (byte)' ' ) )
					len--;
				if( len >= 2 )
				{
					string token = Encoding.ASCII.GetString( header, start, len ).Trim( '_', ' ' );
					if( token.Length >= 2 && token.Length <= 4 )
					{
						string ext = token.ToLowerInvariant();
						if( token.Equals( "TIM2", StringComparison.OrdinalIgnoreCase ) )
							return ".tm2";
						if( token.Equals( "CLT2", StringComparison.OrdinalIgnoreCase ) )
							return ".cl2";
						if( KnownTokens.Contains( token ) || IsSaneToken( ext ) )
						{
							if( !ext.Equals( "pk4", StringComparison.OrdinalIgnoreCase ) &&
								!ext.Equals( "phf", StringComparison.OrdinalIgnoreCase ) )
								return "." + ext;
						}
					}
				}
			}

			return ".bin";
		}

		public static string GuessFromStream( Stream s, long pos, uint maxPeek = 32 )
		{
			if( !s.CanSeek )
				return ".bin";
			long save = s.Position;
			try
			{
				int want = (int)Math.Min( maxPeek, 128 );
				var buf = new byte[ want ];
				s.Position = pos;
				int got = s.Read( buf, 0, want );
				if( got <= 0 )
					return ".bin";
				if( got != buf.Length )
				{
					var trimmed = new byte[ got ];
					Array.Copy( buf, trimmed, got );
					return GuessFromHeader( trimmed );
				}
				return GuessFromHeader( buf );
			}
			finally { s.Position = save; }
		}

		private static bool IsSaneToken( string ext )
		{
			if( ext.Length < 2 || ext.Length > 4 )
				return false;
			for( int i = 0; i < ext.Length; i++ )
			{
				char c = ext[ i ];
				bool ok = ( c >= 'a' && c <= 'z' ) || ( c >= '0' && c <= '9' ) || c == '_';
				if( !ok )
					return false;
			}
			return true;
		}
	}
}
