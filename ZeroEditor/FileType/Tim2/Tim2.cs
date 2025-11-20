using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ZeroEditor.Tim2
{
	public enum Tim2ColorType : byte
	{
		NO_CLUT = 0,
		RGBA16 = 1,
		RGB32 = 2,
		RGBA32 = 3,
		IDTEX4 = 4,
		IDTEX8 = 5
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	public struct TIM2_FILEHEADER
	{
		public byte FileId0, FileId1, FileId2, FileId3;
		public byte Version;
		public byte FormatId;
		public ushort Pictures;
		public ulong Pad;
		public bool IsTIM2 => FileId0 == 'T' && FileId1 == 'I' && FileId2 == 'M' && FileId3 == '2';
		public bool IsCLT2 => FileId0 == 'C' && FileId1 == 'L' && FileId2 == 'T' && FileId3 == '2';
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	public struct TIM2_PICTUREHEADER
	{
		public uint TotalSize;
		public uint ClutDataSize;
		public uint ImageDataSize;
		public ushort HeaderSize;
		public ushort ClutColorsCount;
		public byte PictFormat;
		public byte MipMapTexturesCount;
		public byte ClutColorType;
		public byte ImageColorType;
		public ushort ImageWidth;
		public ushort ImageHeight;
		public ulong GsTex0;
		public ulong GsTex1;
		public uint GsTexaFbaPabe;
		public uint GsTexClut;
	}

	internal static class Tim2Util
	{
		public static T ReadStruct<T>( BinaryReader br ) where T : struct
		{
			int size = Marshal.SizeOf<T>();
			var bytes = br.ReadBytes( size );
			if( bytes.Length != size )
				throw new EndOfStreamException( $"TIM2: expected {size} bytes, got {bytes.Length}." );
			var h = GCHandle.Alloc( bytes, GCHandleType.Pinned );
			try
			{ return Marshal.PtrToStructure<T>( h.AddrOfPinnedObject() )!; }
			finally { h.Free(); }
		}

		public static void WriteStruct<T>( BinaryWriter bw, T s ) where T : struct
		{
			int size = Marshal.SizeOf<T>();
			var buf = new byte[ size ];
			var h = GCHandle.Alloc( buf, GCHandleType.Pinned );
			try
			{
				Marshal.StructureToPtr( s, h.AddrOfPinnedObject(), false );
				bw.Write( buf );
			}
			finally { h.Free(); }
		}

		public static int Align( int size, int align ) => ( size + ( align - 1 ) ) & ~( align - 1 );
		public static int Align16( int n ) => Align( n, 16 );

		public static int BytesPerClutColor( Tim2ColorType clutType ) =>
			clutType switch
			{
				Tim2ColorType.RGBA16 => 2,
				Tim2ColorType.RGB32 => 3,
				Tim2ColorType.RGBA32 => 4,
				_ => 0
			};

		public static bool IsCsm1( ulong gsTex0 ) => ( ( gsTex0 >> 55 ) & 1UL ) == 0UL;

		public static int RemapClutIndex32Bank( int idx )
		{
			int low = idx & 31;
			if( low >= 8 && low < 16 )
				return idx + 8;
			if( low >= 16 && low < 24 )
				return idx - 8;
			return idx;
		}

		public static int RemapClutIndexWrite( int idx, int perSet )
		{
			if( perSet < 32 )
				return idx;
			return RemapClutIndex32Bank( idx );
		}

		public static byte Mul2Clamp( byte a ) => (byte)( a > 127 ? 255 : a * 2 );
		public static byte Div2( byte a ) => (byte)( a >> 1 );
	}

	public sealed class Tim2Image
	{
		public TIM2_FILEHEADER FileHeader;
		public TIM2_PICTUREHEADER Pic;
		public byte[]? Clut;
		public byte[] Image = Array.Empty<byte>();
		public int Width => Pic.ImageWidth;
		public int Height => Pic.ImageHeight;
		public Tim2ColorType ImgType => (Tim2ColorType)Pic.ImageColorType;
		public Tim2ColorType ClutType => (Tim2ColorType)( Pic.ClutColorType & 0x3F );

		public static Tim2Image Load( Stream s )
		{
			if( s is null )
				throw new ArgumentNullException( nameof( s ) );
			using var br = new BinaryReader( s, System.Text.Encoding.ASCII, leaveOpen: true );
			long basePos = s.Position;
			var fh = Tim2Util.ReadStruct<TIM2_FILEHEADER>( br );

			if( !( fh.IsTIM2 || fh.IsCLT2 ) )
				throw new InvalidDataException( "Not a TIM2/CLT2 file." );

			if( !( fh.Version == 0x03 || fh.Version == 0x04 ) )
				throw new InvalidDataException( $"Unsupported TIM2 version: {fh.Version}." );

			long pic0Off = fh.FormatId == 0x00 ? Marshal.SizeOf<TIM2_FILEHEADER>() : 0x80;
			if( basePos + pic0Off > s.Length )
				throw new EndOfStreamException( "TIM2: picture header offset beyond EOF." );

			s.Position = basePos + pic0Off;
			var ph = Tim2Util.ReadStruct<TIM2_PICTUREHEADER>( br );
			long afterHeader = basePos + pic0Off + ph.HeaderSize;

			if( afterHeader < 0 || afterHeader > s.Length )
				throw new EndOfStreamException( "TIM2: header size points beyond EOF." );

			s.Position = afterHeader;

			byte[] image = ph.ImageDataSize > 0
				? br.ReadBytes( checked((int)Math.Min( (long)ph.ImageDataSize, s.Length - s.Position )) )
				: Array.Empty<byte>();

			byte[]? clut = ph.ClutDataSize > 0
				? br.ReadBytes( checked((int)Math.Min( (long)ph.ClutDataSize, s.Length - s.Position )) )
				: null;

			return new Tim2Image
			{
				FileHeader = fh,
				Pic = ph,
				Image = image,
				Clut = clut
			};
		}
	}
}
