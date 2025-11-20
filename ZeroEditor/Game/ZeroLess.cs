using System;
using System.Buffers.Binary;

namespace ZeroEditor.Compress
{
	public sealed class ZeroLess
	{
		public static readonly ZeroLess Instance = new ZeroLess();

		private ZeroLess() { }

		private const int BUFFER_SIZE = 4096;
		private const int MATCH_LENGTH = 18;
		private const int THRESHOLD = 2;

		private const uint MAGIC_LESS = 0x5353454C;
		private const uint MAGIC_USER = 0x52455355;

		public bool TryDecompress( ReadOnlySpan<byte> buf, out byte[] decoded )
		{
			decoded = Array.Empty<byte>();
			if( buf.Length < 0x20 )
				return false;

			var h = new CMP_HEADER( buf );
			if( !( h.Ext == MAGIC_LESS || h.Ext == MAGIC_USER ) )
				return false;
			if( h.DivNum <= 0 || h.DivSize <= 0 || h.Size < 0 )
				return false;

			int baseOff = 0;
			int dataOff = h.DataOffset;
			int divPtr = h.DivP;

			if( h.Mapping == 0 )
			{
				dataOff = baseOff + h.DataOffset;
				divPtr = baseOff + h.DivP;
			}
			if( (uint)divPtr + (uint)h.DivNum * 4u > (uint)buf.Length )
				return false;

			var divs = new ENCODE_DIV_SECTION[ h.DivNum ];
			for( int i = 0, p = divPtr; i < h.DivNum; i++, p += 4 )
				divs[ i ] = new ENCODE_DIV_SECTION( buf.Slice( p, 4 ) );

			int outCap = checked(h.DivNum * h.DivSize);
			var outBytes = new byte[ outCap ];
			int outCursor = 0;

			int cur = dataOff;
			for( int i = 0; i < h.DivNum; i++ )
			{
				cur = Align16( cur );
				if( (uint)cur > (uint)buf.Length )
					return false;

				var d = divs[ i ];
				if( (uint)d.Size > (uint)buf.Length - (uint)cur )
					return false;

				var enc = buf.Slice( cur, d.Size );
				var outSpan = outBytes.AsSpan( outCursor, Math.Min( h.DivSize, outBytes.Length - outCursor ) );

				if( d.Type == ENCODE_TYPE_SLIDE )
				{
					int wrote = SlideDecode( enc, outSpan, d.Size );
					if( wrote < 0 )
						return false;
				}
				else
				{
					if( d.Size > outSpan.Length )
						return false;
					enc.CopyTo( outSpan );
				}

				outCursor += h.DivSize;
				cur += Align16( d.Size );
			}

			if( h.Size <= outBytes.Length )
			{
				if( h.Size == outBytes.Length )
				{
					decoded = outBytes;
					return true;
				}

				decoded = new byte[ h.Size ];
				Buffer.BlockCopy( outBytes, 0, decoded, 0, h.Size );
				return true;
			}

			decoded = outBytes;
			return true;
		}

		public bool TryCompress( ReadOnlySpan<byte> plain, out byte[] encoded, int divSize = 0x4000 )
		{
			encoded = Array.Empty<byte>();

			if( plain.Length == 0 )
				return false;
			if( divSize <= 0 || divSize > 0x100000 )
				throw new ArgumentOutOfRangeException( nameof( divSize ) );

			int divNum = ( plain.Length + divSize - 1 ) / divSize;
			if( divNum <= 0 )
				return false;

			var divs = new ENCODE_DIV_SECTION[ divNum ];
			var blocks = new byte[ divNum ][];

			for( int i = 0; i < divNum; i++ )
			{
				int offset = i * divSize;
				int len = Math.Min( divSize, plain.Length - offset );
				var slice = plain.Slice( offset, len );

				var slide = SlideEncode( slice );

				if( slide.Length >= slice.Length )
				{
					var raw = new byte[ slice.Length ];
					slice.CopyTo( raw );
					blocks[ i ] = raw;
					divs[ i ] = new ENCODE_DIV_SECTION( 0, (ushort)raw.Length );
				}
				else
				{
					blocks[ i ] = slide;
					divs[ i ] = new ENCODE_DIV_SECTION( ENCODE_TYPE_SLIDE, (ushort)slide.Length );
				}
			}

			encoded = BuildLessContainer( plain.Length, divSize, divs, blocks );
			return true;
		}

		public bool TryGetDivSize( ReadOnlySpan<byte> buf, out int divSize )
		{
			divSize = 0;
			if( buf.Length < 0x20 )
				return false;
			var h = new CMP_HEADER( buf );
			if( !( h.Ext == MAGIC_LESS || h.Ext == MAGIC_USER ) )
				return false;
			if( h.DivSize <= 0 || h.DivNum <= 0 || h.Size < 0 )
				return false;
			divSize = h.DivSize;
			return true;
		}

		private static int Align16( int v ) => ( v + 15 ) & ~15;

		private static int SlideDecode( ReadOnlySpan<byte> inBuf, Span<byte> outBuf, int encodedSize )
		{
			int remaining = encodedSize;
			int idx = 0;
			int outIndex = 0;
			int flags = 0;
			int lhs;
			int rhs;
			uint r = (uint)( BUFFER_SIZE - MATCH_LENGTH );
			var ring = new byte[ BUFFER_SIZE + MATCH_LENGTH - 1 ];

			while( true )
			{
				if( ( ( flags >>= 1 ) & 0x0100 ) == 0 )
				{
					if( remaining <= 0 || idx >= inBuf.Length )
						break;
					lhs = inBuf[ idx++ ];
					remaining--;
					flags = lhs | 0xFF00;
				}

				flags &= 0xFFFF;

				if( ( flags & 1 ) == 0 )
				{
					if( remaining <= 0 || idx >= inBuf.Length )
						break;
					lhs = inBuf[ idx++ ];
					remaining--;
					if( remaining <= 0 || idx >= inBuf.Length )
						break;
					rhs = inBuf[ idx++ ];
					remaining--;

					lhs |= ( ( rhs & 0xF0 ) << 4 );
					rhs = ( rhs & 0x0F ) + THRESHOLD;

					for( int i = 0; i <= rhs; i++ )
					{
						int c = ring[ ( lhs + i ) & ( BUFFER_SIZE - 1 ) ];
						if( (uint)outIndex < (uint)outBuf.Length )
							outBuf[ outIndex++ ] = (byte)c;

						ring[ r ] = (byte)c;
						r = ( r + 1 ) & ( BUFFER_SIZE - 1 );
					}
				}
				else
				{
					if( remaining <= 0 || idx >= inBuf.Length )
						break;
					lhs = inBuf[ idx++ ];
					remaining--;

					ring[ r ] = (byte)lhs;
					if( (uint)outIndex < (uint)outBuf.Length )
						outBuf[ outIndex++ ] = (byte)lhs;

					r = ( r + 1 ) & ( BUFFER_SIZE - 1 );
				}
			}

			return outIndex;
		}

		private static byte[] SlideEncode( ReadOnlySpan<byte> inBuf )
		{
			const int N = BUFFER_SIZE;
			const int F = MATCH_LENGTH;
			const int NIL = N;

			if( inBuf.Length == 0 )
				return Array.Empty<byte>();

			var text = new byte[ N + F - 1 ];
			var lson = new int[ N + 1 ];
			var rson = new int[ N + 257 ];
			var dad = new int[ N + 1 ];

			int matchPos = 0;
			int matchLen = 0;

			void InitTree()
			{
				for( int i = N + 1; i <= N + 256; i++ )
					rson[ i ] = NIL;
				for( int i = 0; i < N; i++ )
					dad[ i ] = NIL;
			}

			void InsertNode( int r )
			{
				int cmp = 1;
				int p = N + 1 + text[ r ];
				rson[ r ] = NIL;
				lson[ r ] = NIL;
				matchLen = 0;

				while( true )
				{
					if( cmp >= 0 )
					{
						if( rson[ p ] != NIL )
						{
							p = rson[ p ];
						}
						else
						{
							rson[ p ] = r;
							dad[ r ] = p;
							return;
						}
					}
					else
					{
						if( lson[ p ] != NIL )
						{
							p = lson[ p ];
						}
						else
						{
							lson[ p ] = r;
							dad[ r ] = p;
							return;
						}
					}

					int i;
					for( i = 1; i < F; i++ )
					{
						int rIdx = r + i;
						if( rIdx >= N + F - 1 )
							rIdx -= N;
						int pIdx = p + i;
						if( pIdx >= N + F - 1 )
							pIdx -= N;
						cmp = text[ rIdx ] - text[ pIdx ];
						if( cmp != 0 )
							break;
					}

					if( i > matchLen )
					{
						matchPos = p;
						matchLen = i;
						if( matchLen >= F )
							break;
					}
				}

				dad[ r ] = dad[ p ];
				lson[ r ] = lson[ p ];
				rson[ r ] = rson[ p ];

				if( lson[ r ] != NIL )
					dad[ lson[ r ] ] = r;
				if( rson[ r ] != NIL )
					dad[ rson[ r ] ] = r;

				if( rson[ dad[ p ] ] == p )
					rson[ dad[ p ] ] = r;
				else
					lson[ dad[ p ] ] = r;

				dad[ p ] = NIL;
			}

			void DeleteNode( int p )
			{
				if( dad[ p ] == NIL )
					return;

				int q;
				if( rson[ p ] == NIL )
				{
					q = lson[ p ];
				}
				else if( lson[ p ] == NIL )
				{
					q = rson[ p ];
				}
				else
				{
					q = lson[ p ];
					if( rson[ q ] != NIL )
					{
						do
						{
							q = rson[ q ];
						} while( rson[ q ] != NIL );

						rson[ dad[ q ] ] = lson[ q ];
						if( lson[ q ] != NIL )
							dad[ lson[ q ] ] = dad[ q ];

						lson[ q ] = lson[ p ];
						dad[ lson[ p ] ] = q;
					}

					rson[ q ] = rson[ p ];
					dad[ rson[ p ] ] = q;
				}

				dad[ q ] = dad[ p ];
				if( rson[ dad[ p ] ] == p )
					rson[ dad[ p ] ] = q;
				else
					lson[ dad[ p ] ] = q;

				dad[ p ] = NIL;
			}

			int s = 0;
			int rPos = N - F;
			for( int i = 0; i < rPos; i++ )
				text[ i ] = 0;

			int len = Math.Min( F, inBuf.Length );
			int srcIdx = 0;
			for( int i = 0; i < len; i++ )
				text[ rPos + i ] = inBuf[ srcIdx++ ];

			if( len == 0 )
				return Array.Empty<byte>();

			InitTree();
			for( int i = 1; i <= F; i++ )
				InsertNode( ( rPos - i ) & ( N - 1 ) );
			InsertNode( rPos );

			var code = new byte[ 17 ];
			int codePtr = 1;
			byte mask = 1;
			var output = new System.Collections.Generic.List<byte>( inBuf.Length );

			do
			{
				if( matchLen > len )
					matchLen = len;

				if( matchLen <= THRESHOLD )
				{
					matchLen = 1;
					code[ 0 ] |= mask;
					code[ codePtr++ ] = text[ rPos ];
				}
				else
				{
					int pos = matchPos;
					code[ codePtr++ ] = (byte)pos;
					code[ codePtr++ ] = (byte)( ( ( pos >> 4 ) & 0xF0 ) | ( matchLen - ( THRESHOLD + 1 ) ) );
				}

				mask <<= 1;
				if( mask == 0 )
				{
					for( int i = 0; i < codePtr; i++ )
						output.Add( code[ i ] );
					code[ 0 ] = 0;
					codePtr = 1;
					mask = 1;
				}

				int lastMatchLength = matchLen;
				int iRead;

				for( iRead = 0; iRead < lastMatchLength && srcIdx < inBuf.Length; iRead++ )
				{
					DeleteNode( s );
					byte c = inBuf[ srcIdx++ ];
					text[ s ] = c;
					if( s < F - 1 )
						text[ s + N ] = c;
					s = ( s + 1 ) & ( N - 1 );
					rPos = ( rPos + 1 ) & ( N - 1 );
					InsertNode( rPos );
				}

				while( iRead++ < lastMatchLength )
				{
					DeleteNode( s );
					s = ( s + 1 ) & ( N - 1 );
					rPos = ( rPos + 1 ) & ( N - 1 );
					if( --len != 0 )
						InsertNode( rPos );
				}
			} while( len > 0 );

			if( codePtr > 1 )
			{
				for( int i = 0; i < codePtr; i++ )
					output.Add( code[ i ] );
			}

			return output.ToArray();
		}

		private static byte[] BuildLessContainer( int plainSize, int divSize, ENCODE_DIV_SECTION[] divs, byte[][] blocks )
		{
			if( divs.Length != blocks.Length )
				throw new ArgumentException( "divs/blocks length mismatch" );

			int divNum = divs.Length;

			using var ms = new System.IO.MemoryStream();
			using var bw = new System.IO.BinaryWriter( ms, System.Text.Encoding.ASCII, leaveOpen: true );

			bw.Write( new byte[ 0x20 ] );

			int divPtr = (int)ms.Position;

			for( int i = 0; i < divNum; i++ )
			{
				bw.Write( divs[ i ].Type );
				bw.Write( divs[ i ].Size );
			}

			int afterTable = (int)ms.Position;
			int dataOffset = Align16( afterTable );

			if( dataOffset > afterTable )
				bw.Write( new byte[ dataOffset - afterTable ] );

			for( int i = 0; i < divNum; i++ )
			{
				int cur = (int)ms.Position;
				int aligned = Align16( cur );
				if( aligned > cur )
					bw.Write( new byte[ aligned - cur ] );

				var block = blocks[ i ];
				if( block != null && block.Length > 0 )
					bw.Write( block );

				int endAligned = Align16( (int)ms.Position );
				if( endAligned > ms.Position )
					bw.Write( new byte[ endAligned - (int)ms.Position ] );
			}

			long endPos = ms.Position;
			uint compressedSize = (uint)( endPos - dataOffset );

			ms.Position = 0;

			bw.Write( (uint)plainSize );
			bw.Write( MAGIC_LESS );
			bw.Write( (uint)divSize );
			bw.Write( (uint)divNum );
			bw.Write( (uint)dataOffset );
			bw.Write( (uint)divPtr );
			bw.Write( 0u );
			bw.Write( compressedSize );

			return ms.ToArray();
		}

		private const int ENCODE_TYPE_SLIDE = 1;

		private readonly struct CMP_HEADER
		{
			public readonly int Size;
			public readonly uint Ext;
			public readonly int DivSize;
			public readonly int DivNum;
			public readonly int DataOffset;
			public readonly int DivP;
			public readonly int Mapping;
			public readonly int Compressed;

			public CMP_HEADER( ReadOnlySpan<byte> s )
			{
				Size = (int)BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x00, 4 ) );
				Ext = BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x04, 4 ) );
				DivSize = (int)BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x08, 4 ) );
				DivNum = (int)BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x0C, 4 ) );
				DataOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x10, 4 ) );
				DivP = (int)BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x14, 4 ) );
				Mapping = (int)BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x18, 4 ) );
				Compressed = (int)BinaryPrimitives.ReadUInt32LittleEndian( s.Slice( 0x1C, 4 ) );
			}
		}

		private readonly struct ENCODE_DIV_SECTION
		{
			public readonly short Type;
			public readonly ushort Size;

			public ENCODE_DIV_SECTION( ReadOnlySpan<byte> s )
			{
				Type = (short)BinaryPrimitives.ReadInt16LittleEndian( s.Slice( 0, 2 ) );
				Size = BinaryPrimitives.ReadUInt16LittleEndian( s.Slice( 2, 2 ) );
			}

			public ENCODE_DIV_SECTION( short type, ushort size )
			{
				Type = type;
				Size = size;
			}
		}
	}
}
