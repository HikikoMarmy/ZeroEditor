using System;
using System.IO;

namespace ZeroEditor.Audio
{
	public sealed class AdpcmAudio
	{
		public int Interleave { get; }
		public int Channels { get; }
		public int SampleRate { get; }

		public AdpcmAudio( int interleave = 0x800, int channels = 2, int sampleRate = 48000 )
		{
			if( channels != 1 && channels != 2 )
				throw new ArgumentOutOfRangeException( nameof( channels ) );

			Interleave = interleave;
			Channels = channels;
			SampleRate = sampleRate;
		}

		private static readonly int[][] Coefs =
		{
			new[] {   0,   0 },
			new[] {  60,   0 },
			new[] { 115, -52 },
			new[] {  98, -55 },
			new[] { 122, -60 },
		};

		public MemoryStream DecodeToWavStream( string path )
		{
			byte[] data = File.ReadAllBytes( path );
			var pcm = DecodeToPcm16( data, 0, data.Length );
			return BuildWavStream( pcm, SampleRate, (short)Channels );
		}

		public MemoryStream DecodeRangeToWavStream( string path, int offset, int length )
		{
			byte[] data = File.ReadAllBytes( path );
			if( offset < 0 || offset > data.Length )
				throw new ArgumentOutOfRangeException( nameof( offset ) );
			if( length < 0 )
				length = 0;
			if( offset + length > data.Length )
				length = data.Length - offset;
			if( length <= 0 )
				return BuildWavStream( Array.Empty<short>(), SampleRate, (short)Channels );
			var pcm = DecodeToPcm16( data, offset, length );
			return BuildWavStream( pcm, SampleRate, (short)Channels );
		}

		public MemoryStream DecodeToRawPcm16Stream( string path )
		{
			byte[] data = File.ReadAllBytes( path );
			var pcm = DecodeToPcm16( data, 0, data.Length );

			var ms = new MemoryStream( pcm.Length * 2 );
			using var bw = new BinaryWriter( ms, System.Text.Encoding.UTF8, leaveOpen: true );
			foreach( short s in pcm )
				bw.Write( s );
			ms.Position = 0;
			return ms;
		}

		private short[] DecodeToPcm16( byte[] file, int offset, int length )
		{
			const int frameSize = 16;
			const int samplesPerFrame = 28;

			if( offset < 0 || offset > file.Length )
				throw new ArgumentOutOfRangeException( nameof( offset ) );
			if( length < 0 )
				length = 0;
			if( offset + length > file.Length )
				length = file.Length - offset;

			int pos = offset;
			int adpcmBytes = length;

			int framesPerBlock = Interleave / frameSize;
			if( framesPerBlock <= 0 )
				framesPerBlock = 1;

			int samplesPerBlockPerCh = framesPerBlock * samplesPerFrame;

			int totalBlocks = ( adpcmBytes + Interleave - 1 ) / Interleave;
			if( Channels == 2 )
				totalBlocks = ( totalBlocks / 2 ) * 2;
			if( totalBlocks <= 0 && adpcmBytes > 0 )
				totalBlocks = Channels == 2 ? 2 : 1;

			long totalSamplesPerCh = (long)( totalBlocks / Math.Max( 1, Channels ) ) * samplesPerBlockPerCh;
			long totalSamples = totalSamplesPerCh * Channels;

			var pcm = new short[ totalSamples ];

			int[] s1 = new int[ Channels ];
			int[] s2 = new int[ Channels ];
			long writeIndex = 0;

			if( Channels == 1 )
			{
				for( int b = 0; b < totalBlocks; b++, pos += Interleave )
				{
					int blockSize = Math.Min( Interleave, offset + length - pos );
					if( blockSize <= 0 )
						break;

					int wrote = DecodeInterleaveBlock(
						file, pos, blockSize, 0,
						pcm, writeIndex, 1,
						ref s1[ 0 ], ref s2[ 0 ] );

					writeIndex += wrote;
				}
			}
			else
			{
				for( int b = 0; b < totalBlocks; b += 2 )
				{
					long baseIdx = writeIndex;

					int blockSizeL = Math.Min( Interleave, offset + length - pos );
					if( blockSizeL <= 0 )
						break;
					int wroteL = DecodeInterleaveBlock(
						file, pos, blockSizeL, 0,
						pcm, baseIdx, 2,
						ref s1[ 0 ], ref s2[ 0 ] );
					pos += Interleave;

					int blockSizeR = Math.Min( Interleave, offset + length - pos );
					if( blockSizeR <= 0 )
						break;
					int wroteR = DecodeInterleaveBlock(
						file, pos, blockSizeR, 1,
						pcm, baseIdx, 2,
						ref s1[ 1 ], ref s2[ 1 ] );
					pos += Interleave;

					int wrote = Math.Min( wroteL, wroteR );
					writeIndex += (long)wrote * 2;
				}
			}

			if( writeIndex < pcm.Length )
				Array.Resize( ref pcm, (int)writeIndex );

			return pcm;
		}

		private int DecodeInterleaveBlock(
			byte[] src, int blockOffset, int blockSize, int channelIndex,
			short[] pcm, long baseIndex, int channels,
			ref int s1, ref int s2 )
		{
			const int frameSize = 16;
			const int samplesPerFrame = 28;

			int framesPerBlock = blockSize / frameSize;
			if( framesPerBlock <= 0 )
				return 0;

			int totalSamples = framesPerBlock * samplesPerFrame;

			Span<short> temp = stackalloc short[ totalSamples ];
			int tIdx = 0;

			for( int f = 0; f < framesPerBlock; f++ )
			{
				var frame = new ReadOnlySpan<byte>( src, blockOffset + f * frameSize, frameSize );
				DecodeFrame( frame, temp, ref tIdx, ref s1, ref s2 );
			}

			long dst = baseIndex + channelIndex;
			for( int i = 0; i < tIdx; i++, dst += channels )
			{
				if( (ulong)dst < (ulong)pcm.Length )
					pcm[ dst ] = temp[ i ];
				else
					break;
			}

			return tIdx;
		}

		private void DecodeFrame( ReadOnlySpan<byte> frame, Span<short> dst, ref int dstIndex, ref int s1, ref int s2 )
		{
			int header = frame[ 0 ];
			int shift = header & 0x0F;
			if( shift > 12 )
				shift = 9;

			int filter = ( header >> 4 ) & 0x07;
			if( filter > 4 )
				filter = 4;

			int c0 = Coefs[ filter ][ 0 ];
			int c1 = Coefs[ filter ][ 1 ];

			for( int i = 0; i < 28; i++ )
			{
				byte b = frame[ 2 + ( i >> 1 ) ];
				int nibble = ( ( i & 1 ) == 0 ) ? ( b & 0x0F ) : ( b >> 4 );
				int s = ( nibble << 28 ) >> 28;

				int sample = ( s << ( 12 - shift ) );
				sample += ( ( c0 * s1 ) + ( c1 * s2 ) + 32 ) >> 6;

				if( sample > 32767 )
					sample = 32767;
				if( sample < -32768 )
					sample = -32768;

				s2 = s1;
				s1 = sample;

				dst[ dstIndex++ ] = (short)sample;
			}
		}

		private MemoryStream BuildWavStream( short[] pcm, int sampleRate, short channels )
		{
			int dataSize = pcm.Length * 2;
			var ms = new MemoryStream( 44 + dataSize );

			using var bw = new BinaryWriter( ms, System.Text.Encoding.UTF8, leaveOpen: true );

			int byteRate = sampleRate * channels * 2;
			short blockAlign = (short)( channels * 2 );

			bw.Write( System.Text.Encoding.ASCII.GetBytes( "RIFF" ) );
			bw.Write( 36 + dataSize );
			bw.Write( System.Text.Encoding.ASCII.GetBytes( "WAVE" ) );
			bw.Write( System.Text.Encoding.ASCII.GetBytes( "fmt " ) );
			bw.Write( 16 );
			bw.Write( (short)1 );
			bw.Write( channels );
			bw.Write( sampleRate );
			bw.Write( byteRate );
			bw.Write( blockAlign );
			bw.Write( (short)16 );

			bw.Write( System.Text.Encoding.ASCII.GetBytes( "data" ) );
			bw.Write( dataSize );

			foreach( var s in pcm )
				bw.Write( s );

			ms.Position = 0;
			return ms;
		}

		public void EncodeWavToAdpcmFile( string wavPath, string outPath )
		{
			ReadPcm16Wav( wavPath, out var wavCh, out var wavRate, out var pcmL, out var pcmR );

			if( wavCh != Channels )
				throw new InvalidOperationException( $"WAV channels {wavCh} != expected {Channels}" );
			if( wavRate != SampleRate )
				throw new InvalidOperationException( $"WAV sample rate {wavRate} != expected {SampleRate}" );

			short[] ch0 = pcmL ?? Array.Empty<short>();
			short[] ch1 = Channels == 2 ? ( pcmR ?? Array.Empty<short>() ) : Array.Empty<short>();

			using var fs = new FileStream( outPath, FileMode.Create, FileAccess.Write, FileShare.None );
			using var bw = new BinaryWriter( fs );

			EncodeChannelsToStream( ch0, ch1, bw );
		}

		public byte[] EncodeWavToAdpcmBytes( string wavPath )
		{
			ReadPcm16Wav( wavPath, out var wavCh, out var wavRate, out var pcmL, out var pcmR );

			if( wavCh != Channels )
				throw new InvalidOperationException( $"WAV channels {wavCh} != expected {Channels}" );
			if( wavRate != SampleRate )
				throw new InvalidOperationException( $"WAV sample rate {wavRate} != expected {SampleRate}" );

			short[] ch0 = pcmL ?? Array.Empty<short>();
			short[] ch1 = Channels == 2 ? ( pcmR ?? Array.Empty<short>() ) : Array.Empty<short>();

			using var ms = new MemoryStream();
			using var bw = new BinaryWriter( ms, System.Text.Encoding.UTF8, leaveOpen: true );
			EncodeChannelsToStream( ch0, ch1, bw );
			bw.Flush();
			return ms.ToArray();
		}

		private void EncodeChannelsToStream( short[] ch0, short[] ch1, BinaryWriter bw )
		{
			const int frameSize = 16;

			int framesPerBlock = Math.Max( 1, Interleave / frameSize );

			int pos0 = 0, pos1 = 0;
			int s1_0 = 0, s2_0 = 0;
			int s1_1 = 0, s2_1 = 0;

			if( Channels == 1 )
			{
				while( pos0 < ch0.Length )
				{
					int bytesBefore = (int)bw.BaseStream.Position;
					EncodeOneChannelBlock( ch0, ref pos0, framesPerBlock, ref s1_0, ref s2_0, bw );
					int wrote = (int)bw.BaseStream.Position - bytesBefore;

					if( wrote < Interleave )
						WriteSilentFrames( Interleave - wrote, bw );
				}
			}
			else
			{
				while( pos0 < ch0.Length || pos1 < ch1.Length )
				{
					int bytesBeforeL = (int)bw.BaseStream.Position;
					EncodeOneChannelBlock( ch0, ref pos0, framesPerBlock, ref s1_0, ref s2_0, bw );
					int wroteL = (int)bw.BaseStream.Position - bytesBeforeL;
					if( wroteL < Interleave )
						WriteSilentFrames( Interleave - wroteL, bw );

					int bytesBeforeR = (int)bw.BaseStream.Position;
					EncodeOneChannelBlock( ch1, ref pos1, framesPerBlock, ref s1_1, ref s2_1, bw );
					int wroteR = (int)bw.BaseStream.Position - bytesBeforeR;
					if( wroteR < Interleave )
						WriteSilentFrames( Interleave - wroteR, bw );
				}
			}
		}

		private void EncodeOneChannelBlock( short[] src, ref int pos, int framesPerBlock, ref int s1, ref int s2, BinaryWriter bw )
		{
			const int samplesPerFrame = 28;

			for( int f = 0; f < framesPerBlock; f++ )
			{
				Span<int> in28 = stackalloc int[ samplesPerFrame ];
				int got = 0;

				while( got < samplesPerFrame && pos < src.Length )
					in28[ got++ ] = src[ pos++ ];

				if( got < samplesPerFrame )
				{
					for( int i = got; i < samplesPerFrame; i++ )
						in28[ i ] = 0;
				}

				var frame = EncodeFrame( in28, ref s1, ref s2 );
				bw.Write( frame );

				if( pos >= src.Length && got == 0 )
					break;
			}
		}

		private void WriteSilentFrames( int bytesToPad, BinaryWriter bw )
		{
			Span<byte> silence = stackalloc byte[ 16 ];
			silence.Clear();
			silence[ 0 ] = (byte)( ( 0 << 4 ) | 12 );

			while( bytesToPad > 0 )
			{
				int n = Math.Min( bytesToPad, 16 );
				if( n == 16 )
					bw.Write( silence );
				else
					bw.Write( silence[ ..n ].ToArray() );

				bytesToPad -= n;
			}
		}

		private byte[] EncodeFrame( Span<int> in28, ref int s1, ref int s2 )
		{
			int bestFilter = 0, bestShift = 12;
			long bestErr = long.MaxValue;

			Span<sbyte> bestNibbles = stackalloc sbyte[ 28 ];

			for( int filter = 0; filter <= 4; filter++ )
			{
				int c0 = Coefs[ filter ][ 0 ];
				int c1 = Coefs[ filter ][ 1 ];

				for( int shift = 0; shift <= 12; shift++ )
				{
					int ls1 = s1, ls2 = s2;
					long err = 0;
					bool ok = true;

					Span<sbyte> nibbles = stackalloc sbyte[ 28 ];

					for( int i = 0; i < 28; i++ )
					{
						int predicted = ( ( c0 * ls1 ) + ( c1 * ls2 ) + 32 ) >> 6;
						int resid = in28[ i ] - predicted;

						int q = ( resid + ( 1 << ( 11 - shift ) ) ) >> ( 12 - shift );
						if( q < -8 || q > 7 )
						{
							ok = false;
							break;
						}

						int recon = ( q << ( 12 - shift ) ) + predicted;
						if( recon > 32767 )
							recon = 32767;
						if( recon < -32768 )
							recon = -32768;

						int e = in28[ i ] - recon;
						err += (long)e * e;

						ls2 = ls1;
						ls1 = recon;

						nibbles[ i ] = (sbyte)q;
					}

					if( ok && err < bestErr )
					{
						bestErr = err;
						bestFilter = filter;
						bestShift = shift;
						nibbles.CopyTo( bestNibbles );
					}
				}
			}

			byte[] out16 = new byte[ 16 ];
			out16[ 0 ] = (byte)( ( bestFilter << 4 ) | ( bestShift & 0x0F ) );
			out16[ 1 ] = 0x00;

			for( int i = 0; i < 28; i += 2 )
			{
				int lo = bestNibbles[ i ] & 0x0F;
				int hi = bestNibbles[ i + 1 ] & 0x0F;
				out16[ 2 + ( i >> 1 ) ] = (byte)( ( hi << 4 ) | lo );
			}

			int c0b = Coefs[ bestFilter ][ 0 ];
			int c1b = Coefs[ bestFilter ][ 1 ];
			int ls1b = s1, ls2b = s2;

			for( int i = 0; i < 28; i++ )
			{
				int predicted = ( ( c0b * ls1b ) + ( c1b * ls2b ) + 32 ) >> 6;
				int recon = ( bestNibbles[ i ] << ( 12 - bestShift ) ) + predicted;

				if( recon > 32767 )
					recon = 32767;
				if( recon < -32768 )
					recon = -32768;

				ls2b = ls1b;
				ls1b = recon;
			}

			s2 = ls2b;
			s1 = ls1b;

			return out16;
		}

		private static void ReadPcm16Wav(
			string wavPath,
			out int channels,
			out int sampleRate,
			out short[]? ch0,
			out short[]? ch1 )
		{
			using var fs = new FileStream( wavPath, FileMode.Open, FileAccess.Read, FileShare.Read );
			using var br = new BinaryReader( fs );

			if( br.ReadUInt32() != 0x46464952 )
				throw new InvalidDataException( "Not a RIFF file." );
			br.ReadUInt32();
			if( br.ReadUInt32() != 0x45564157 )
				throw new InvalidDataException( "Not a WAVE file." );

			ushort audioFmt = 0;
			channels = 0;
			sampleRate = 0;
			ushort bitsPerSample = 0;
			int dataLen = 0;
			long dataPos = 0;

			while( br.BaseStream.Position + 8 <= br.BaseStream.Length )
			{
				uint id = br.ReadUInt32();
				int len = br.ReadInt32();

				if( id == 0x20746D66 )
				{
					audioFmt = br.ReadUInt16();
					channels = br.ReadUInt16();
					sampleRate = br.ReadInt32();
					int _byteRate = br.ReadInt32();
					ushort _blockAlign = br.ReadUInt16();
					bitsPerSample = br.ReadUInt16();

					int extra = len - 16;
					if( extra > 0 )
						br.BaseStream.Position += extra;
				}
				else if( id == 0x61746164 )
				{
					dataPos = br.BaseStream.Position;
					dataLen = len;
					br.BaseStream.Position += len;
				}
				else
				{
					br.BaseStream.Position += len;
				}
			}

			if( audioFmt != 1 || bitsPerSample != 16 )
				throw new InvalidDataException( "WAV must be PCM16." );
			if( channels != 1 && channels != 2 )
				throw new InvalidDataException( "WAV must be mono or stereo." );
			if( dataPos == 0 || dataLen <= 0 )
				throw new InvalidDataException( "WAV has no data chunk." );

			br.BaseStream.Position = dataPos;
			int samples = dataLen / 2 / channels;

			ch0 = new short[ samples ];
			ch1 = channels == 2 ? new short[ samples ] : null;

			for( int i = 0; i < samples; i++ )
			{
				short sL = br.ReadInt16();
				if( channels == 2 )
				{
					short sR = br.ReadInt16();
					ch0![ i ] = sL;
					ch1![ i ] = sR;
				}
				else
				{
					ch0![ i ] = sL;
				}
			}
		}
	}
}
