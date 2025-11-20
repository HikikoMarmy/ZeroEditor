using System;
using System.Collections.Generic;
using System.IO;

namespace ZeroEditor.Audio
{
	public enum AdpcmKind
	{
		Str,
		Bd
	}

	public sealed class AdpcmFormat
	{
		public AdpcmKind Kind { get; }
		public int Interleave { get; }
		public int Channels { get; }
		public int SampleRate { get; }

		public AdpcmFormat( AdpcmKind kind, int interleave, int channels, int sampleRate )
		{
			if( channels != 1 && channels != 2 )
				throw new ArgumentOutOfRangeException( nameof( channels ) );
			if( interleave <= 0 )
				throw new ArgumentOutOfRangeException( nameof( interleave ) );
			if( sampleRate <= 0 )
				throw new ArgumentOutOfRangeException( nameof( sampleRate ) );

			Kind = kind;
			Interleave = interleave;
			Channels = channels;
			SampleRate = sampleRate;
		}

		public override string ToString()
			=> $"{Kind}  Ch={Channels}, {SampleRate} Hz, Interleave=0x{Interleave:X}";
	}

	public sealed class AdpcmClipInfo
	{
		public int Index { get; }
		public int SampleRate { get; }
		public int OffsetBytes { get; }
		public int LengthBytes { get; }
		public int LoopStart { get; }
		public int LoopEnd { get; }
		public ushort Attr { get; }
		public short Pan { get; }
		public ushort Adsr1 { get; }
		public ushort Adsr2 { get; }

		public AdpcmClipInfo(
			int index,
			int sampleRate,
			int offsetBytes,
			int lengthBytes,
			int loopStart,
			int loopEnd,
			ushort attr,
			short pan,
			ushort adsr1,
			ushort adsr2 )
		{
			Index = index;
			SampleRate = sampleRate;
			OffsetBytes = offsetBytes;
			LengthBytes = lengthBytes;
			LoopStart = loopStart;
			LoopEnd = loopEnd;
			Attr = attr;
			Pan = pan;
			Adsr1 = adsr1;
			Adsr2 = adsr2;
		}

		public override string ToString()
		{
			return $"{Index}: {SampleRate} Hz  Offset=0x{OffsetBytes:X}  Length=0x{LengthBytes:X}";
		}
	}

	public sealed class AdpcmBank
	{
		public string AdpcmPath { get; }
		public string? HxdPath { get; }
		public int InterleaveBytes { get; }
		public IReadOnlyList<AdpcmClipInfo> Clips { get; }

		public AdpcmBank( string adpcmPath, string? hxdPath, int interleaveBytes, IReadOnlyList<AdpcmClipInfo> clips )
		{
			AdpcmPath = adpcmPath;
			HxdPath = hxdPath;
			InterleaveBytes = interleaveBytes;
			Clips = clips;
		}
	}

	public static class AdpcmFormatResolver
	{
		public static AdpcmFormat ForPath( string path )
		{
			if( path == null )
				throw new ArgumentNullException( nameof( path ) );

			string ext = Path.GetExtension( path ).ToLowerInvariant();
			var kind = ext == ".bd" ? AdpcmKind.Bd : AdpcmKind.Str;

			int channels = kind == AdpcmKind.Str ? 2 : 1;
			int sampleRate = kind == AdpcmKind.Str ? 48000 : 22050;
			int interleave = 0x800;

			if( TryGetHxdSampleRate( path, out var hxdRate ) )
				sampleRate = hxdRate;

			if( kind == AdpcmKind.Bd && TryGetHxdBank( path, out var bank ) )
			{
				if( bank.InterleaveBytes > 0 )
					interleave = bank.InterleaveBytes;
				if( bank.Clips.Count > 0 )
				{
					int r = bank.Clips[ 0 ].SampleRate;
					if( r >= 8000 && r <= 96000 )
						sampleRate = r;
				}
			}

			return new AdpcmFormat( kind, interleave, channels, sampleRate );
		}

		public static bool TryGetHxdSampleRate( string adpcmPath, out int rate )
		{
			rate = 0;
			if( string.IsNullOrEmpty( adpcmPath ) )
				return false;

			string hxdPath = Path.ChangeExtension( adpcmPath, ".hxd" );
			if( !File.Exists( hxdPath ) )
				return false;

			try
			{
				byte[] meta = File.ReadAllBytes( hxdPath );
				if( meta.Length >= 0x24 )
				{
					int r = BitConverter.ToInt32( meta, 0x20 );
					if( r >= 8000 && r <= 96000 )
					{
						rate = r;
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		public static bool TryGetHxdBank( string adpcmPath, out AdpcmBank bank )
		{
			bank = null!;
			if( string.IsNullOrEmpty( adpcmPath ) )
				return false;

			string hxdPath = Path.ChangeExtension( adpcmPath, ".hxd" );
			if( !File.Exists( hxdPath ) )
				return false;
			if( !File.Exists( adpcmPath ) )
				return false;

			string ext = Path.GetExtension( adpcmPath ).ToLowerInvariant();
			int defaultRate = ext == ".bd" ? 22050 : 48000;

			byte[] meta;
			try
			{
				meta = File.ReadAllBytes( hxdPath );
			}
			catch
			{
				return false;
			}

			if( meta.Length < 0x20 )
				return false;

			int name = BitConverter.ToInt32( meta, 0x00 );
			int version = BitConverter.ToInt32( meta, 0x04 );
			int num = BitConverter.ToInt32( meta, 0x08 );
			int type = BitConverter.ToInt32( meta, 0x0C );
			int size = BitConverter.ToInt32( meta, 0x10 );
			int interleave = BitConverter.ToInt32( meta, 0x14 );

			if( num <= 0 || num > 4096 )
				return false;
			if( interleave <= 0 || interleave > 0x10000 )
				interleave = 0x800;

			long adpcmSize;
			try
			{
				adpcmSize = new FileInfo( adpcmPath ).Length;
			}
			catch
			{
				return false;
			}

			if( adpcmSize <= 0 )
				return false;

			const int infoBase = 0x20;
			const int infoSize = 0x1C;

			int maxEntriesBySize = ( meta.Length - infoBase ) / infoSize;
			if( maxEntriesBySize <= 0 )
				return false;
			if( num > maxEntriesBySize )
				num = maxEntriesBySize;

			var smplRates = new int[ num ];
			var offsets = new int[ num ];
			var loopStarts = new int[ num ];
			var loopEnds = new int[ num ];
			var attrs = new ushort[ num ];
			var pans = new short[ num ];
			var adsr1 = new ushort[ num ];
			var adsr2 = new ushort[ num ];

			for( int i = 0; i < num; i++ )
			{
				int off = infoBase + i * infoSize;
				if( off + infoSize > meta.Length )
					return false;

				int sr = BitConverter.ToInt32( meta, off + 0x00 );
				int ofs = BitConverter.ToInt32( meta, off + 0x04 );
				ushort pitch = BitConverter.ToUInt16( meta, off + 0x08 );
				ushort vol = BitConverter.ToUInt16( meta, off + 0x0A );
				ushort a1 = BitConverter.ToUInt16( meta, off + 0x0C );
				ushort a2 = BitConverter.ToUInt16( meta, off + 0x0E );
				ushort attr = BitConverter.ToUInt16( meta, off + 0x10 );
				short pan = BitConverter.ToInt16( meta, off + 0x12 );
				int loopstart = BitConverter.ToInt32( meta, off + 0x14 );
				int loopend = BitConverter.ToInt32( meta, off + 0x18 );

				if( sr == 0 )
					sr = defaultRate;
				if( sr < 8000 || sr > 96000 )
					return false;
				if( ofs < 0 || ofs >= adpcmSize )
					return false;

				smplRates[ i ] = sr;
				offsets[ i ] = ofs;
				loopStarts[ i ] = loopstart;
				loopEnds[ i ] = loopend;
				attrs[ i ] = attr;
				pans[ i ] = pan;
				adsr1[ i ] = a1;
				adsr2[ i ] = a2;
			}

			var sortedOffsets = new int[ num ];
			Array.Copy( offsets, sortedOffsets, num );
			Array.Sort( sortedOffsets );

			var clips = new List<AdpcmClipInfo>( num );

			for( int i = 0; i < num; i++ )
			{
				int ofs = offsets[ i ];
				int clipEnd = (int)adpcmSize;

				for( int k = 0; k < sortedOffsets.Length; k++ )
				{
					int cand = sortedOffsets[ k ];
					if( cand > ofs )
					{
						clipEnd = cand;
						break;
					}
				}

				if( clipEnd < ofs )
					clipEnd = ofs;

				int len = clipEnd - ofs;
				if( len <= 0 )
					continue;

				int relLoopStart = loopStarts[ i ];
				int relLoopEnd = loopEnds[ i ];

				var clip = new AdpcmClipInfo(
					i,
					smplRates[ i ],
					ofs,
					len,
					relLoopStart,
					relLoopEnd,
					attrs[ i ],
					pans[ i ],
					adsr1[ i ],
					adsr2[ i ] );
				clips.Add( clip );
			}

			if( clips.Count == 0 )
				return false;

			bank = new AdpcmBank( adpcmPath, hxdPath, interleave, clips );
			return true;
		}

		public static bool TryApplySpliceToHxd( AdpcmBank bank, int changedIndex, int delta )
		{
			if( bank == null )
				return false;
			if( delta == 0 )
				return true;
			if( bank.HxdPath == null )
				return false;
			if( !File.Exists( bank.HxdPath ) )
				return false;
			if( changedIndex < 0 || changedIndex >= bank.Clips.Count )
				return false;

			byte[] meta;
			try
			{
				meta = File.ReadAllBytes( bank.HxdPath );
			}
			catch
			{
				return false;
			}

			if( meta.Length < 0x20 )
				return false;

			int num = BitConverter.ToInt32( meta, 0x08 );
			if( num <= 0 )
				return false;

			const int infoBase = 0x20;
			const int infoSize = 0x1C;

			int maxEntriesBySize = ( meta.Length - infoBase ) / infoSize;
			if( maxEntriesBySize <= 0 )
				return false;
			if( num > maxEntriesBySize )
				num = maxEntriesBySize;

			for( int i = 0; i < num; i++ )
			{
				int off = infoBase + i * infoSize;
				if( off + infoSize > meta.Length )
					return false;

				if( i > changedIndex )
				{
					int ofs = BitConverter.ToInt32( meta, off + 0x04 ) + delta;
					byte[] bOfs = BitConverter.GetBytes( ofs );
					meta[ off + 0x04 ] = bOfs[ 0 ];
					meta[ off + 0x05 ] = bOfs[ 1 ];
					meta[ off + 0x06 ] = bOfs[ 2 ];
					meta[ off + 0x07 ] = bOfs[ 3 ];
				}
			}

			try
			{
				File.WriteAllBytes( bank.HxdPath, meta );
			}
			catch
			{
				return false;
			}

			return true;
		}
	}
}
