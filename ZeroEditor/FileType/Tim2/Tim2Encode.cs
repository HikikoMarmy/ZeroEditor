using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ZeroEditor.Tim2
{
	public static class Tim2Encode
	{
		private struct Rgba32 { public byte R, G, B, A; public Rgba32( byte r, byte g, byte b, byte a ) { R = r; G = g; B = b; A = a; } }

		private static float SrgbToLinear( byte s )
		{
			float v = s / 255f;
			return v <= 0.04045f ? v / 12.92f : (float)Math.Pow( ( v + 0.055f ) / 1.055f, 2.4 );
		}

		private const float Rgba16ExposureGain = 1.30f;
		private const float Rgba16BlackProtect = 0.020f;
		private const float IndexedExposureGain = 1.20f;
		private const float IndexedBlackProtect = 0.015f;

		private static void ApplyGainWithBlackProtect( ref float lr, ref float lg, ref float lb, float gain, float protect )
		{
			float L = 0.2126f * lr + 0.7152f * lg + 0.0722f * lb;
			float w = protect <= 0 ? 1f : MathF.Min( 1f, L / protect );
			float g = 1f + ( gain - 1f ) * w;
			lr = MathF.Min( 1f, lr * g );
			lg = MathF.Min( 1f, lg * g );
			lb = MathF.Min( 1f, lb * g );
		}

		private static readonly byte[,] Bayer8 = new byte[ 8, 8 ]{
			{  0, 48, 12, 60,  3, 51, 15, 63 },
			{ 32, 16, 44, 28, 35, 19, 47, 31 },
			{  8, 56,  4, 52, 11, 59,  7, 55 },
			{ 40, 24, 36, 20, 43, 27, 39, 23 },
			{  2, 50, 14, 62,  1, 49, 13, 61 },
			{ 34, 18, 46, 30, 33, 17, 45, 29 },
			{ 10, 58,  6, 54,  9, 57,  5, 53 },
			{ 42, 26, 38, 22, 41, 25, 37, 21 },
		};

		private static ushort ToRgba5551Dithered( byte r, byte g, byte b, byte a, int x, int y )
		{
			byte pr = (byte)( r * a / 255 );
			byte pg = (byte)( g * a / 255 );
			byte pb = (byte)( b * a / 255 );
			float lr = SrgbToLinear( pr );
			float lg = SrgbToLinear( pg );
			float lb = SrgbToLinear( pb );
			ApplyGainWithBlackProtect( ref lr, ref lg, ref lb, Rgba16ExposureGain, Rgba16BlackProtect );
			float t = ( Bayer8[ y & 7, x & 7 ] + 0.5f ) / 64f;
			const float step = 1f / 31f;
			float d = ( t - 0.5f ) * step;
			lr = MathF.Min( 1f, MathF.Max( 0f, lr + d ) );
			lg = MathF.Min( 1f, MathF.Max( 0f, lg + d ) );
			lb = MathF.Min( 1f, MathF.Max( 0f, lb + d ) );
			int qr = (int)MathF.Round( lr * 31f );
			int qg = (int)MathF.Round( lg * 31f );
			int qb = (int)MathF.Round( lb * 31f );
			int qa = a >= 200 ? 1 : 0;
			return (ushort)( ( qa << 15 ) | ( qb << 10 ) | ( qg << 5 ) | ( qr ) );
		}

		public static void ImportPngOverTruecolor( string tim2Path, string pngPath, string outTim2Path, bool halfAlpha )
		{
			Tim2Image t2;
			using( var fs = new FileStream( tim2Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ) )
			{
				t2 = Tim2Image.Load( fs );
			}
			var imgType = (Tim2ColorType)t2.Pic.ImageColorType;
			if( imgType != Tim2ColorType.RGBA32 && imgType != Tim2ColorType.RGB32 && imgType != Tim2ColorType.RGBA16 )
				throw new NotSupportedException( $"Truecolor only (RGBA16/RGB32/RGBA32). Got {imgType}" );
			using var bmp = new Bitmap( pngPath );
			if( bmp.Width != t2.Width || bmp.Height != t2.Height )
				throw new InvalidDataException( $"PNG size {bmp.Width}x{bmp.Height} != TIM2 {t2.Width}x{t2.Height}" );
			var newImage = ReencodeFromBitmap( bmp, imgType, halveAlpha: halfAlpha && imgType == Tim2ColorType.RGBA32 );
			t2.Pic.ImageDataSize = (uint)newImage.Length;
			string tmp = AtomicIO.CreateTempSibling( outTim2Path );
			using var outFs = new FileStream( tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough );
			using var bw = new BinaryWriter( outFs );
			Tim2Util.WriteStruct( bw, t2.FileHeader );
			long pic0Off = ( t2.FileHeader.FormatId == 0x00 ) ? Marshal.SizeOf<TIM2_FILEHEADER>() : 0x80;
			if( outFs.Position < pic0Off )
				bw.Write( new byte[ pic0Off - outFs.Position ] );
			long phPos = outFs.Position;
			Tim2Util.WriteStruct( bw, t2.Pic );
			bw.Write( newImage );
			int imgPad = Tim2Util.Align16( (int)t2.Pic.ImageDataSize ) - (int)t2.Pic.ImageDataSize;
			if( imgPad != 0 )
				bw.Write( new byte[ imgPad ] );
			if( t2.Pic.ClutDataSize > 0 && t2.Clut != null )
			{
				bw.Write( t2.Clut );
				int clutPad = Tim2Util.Align16( (int)t2.Pic.ClutDataSize ) - (int)t2.Pic.ClutDataSize;
				if( clutPad != 0 )
					bw.Write( new byte[ clutPad ] );
			}
			long endPos = outFs.Position;
			t2.Pic.TotalSize = (uint)( endPos - phPos );
			outFs.Position = phPos;
			Tim2Util.WriteStruct( bw, t2.Pic );
			bw.Flush();
			outFs.Flush( true );
			outFs.Dispose();
			AtomicIO.ReplaceFile( tmp, outTim2Path );
		}

		public static void ImportPngAuto(
			string tim2Path,
			string pngPath,
			string outTim2Path,
			int clutSet,
			bool dither,
			int kMeansIterations,
			bool serpentineDither,
			bool premultiplyForDither,
			bool writeSwizzledClut,
			bool halfAlpha )
		{
			Tim2Image t2peek;
			using( var fs = new FileStream( tim2Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ) )
			{
				t2peek = Tim2Image.Load( fs );
			}
			switch( t2peek.ImgType )
			{
				case Tim2ColorType.RGBA16:
				case Tim2ColorType.RGB32:
				case Tim2ColorType.RGBA32:
				ImportPngOverTruecolor( tim2Path, pngPath, outTim2Path, halfAlpha );
				break;
				case Tim2ColorType.IDTEX8:
				ImportPngOverIndexed8( tim2Path, pngPath, outTim2Path, clutSet, dither, kMeansIterations, serpentineDither, premultiplyForDither, writeSwizzledClut, halfAlpha );
				break;
				case Tim2ColorType.IDTEX4:
				ImportPngOverIndexed4( tim2Path, pngPath, outTim2Path, clutSet, dither, kMeansIterations, serpentineDither, premultiplyForDither, writeSwizzledClut, halfAlpha );
				break;
				default:
				throw new NotSupportedException( $"Unsupported TIM2 type: {t2peek.ImgType}" );
			}
		}

		private static byte[] ReencodeFromBitmap( Bitmap bmp, Tim2ColorType imgType, bool halveAlpha )
		{
			var rect = new Rectangle( 0, 0, bmp.Width, bmp.Height );
			using var argb = bmp.PixelFormat == PixelFormat.Format32bppArgb ? bmp : bmp.Clone( rect, PixelFormat.Format32bppArgb );
			var data = argb.LockBits( rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb );
			try
			{
				int w = argb.Width, h = argb.Height, stride = data.Stride;
				int bpp = imgType switch
				{
					Tim2ColorType.RGBA32 => 4,
					Tim2ColorType.RGB32 => 3,
					Tim2ColorType.RGBA16 => 2,
					_ => throw new NotSupportedException( $"Unsupported truecolor type {imgType}" )
				};
				var outBytes = new byte[ w * h * bpp ];
				unsafe
				{
					byte* src = (byte*)data.Scan0;
					int di = 0;
					for( int y = 0; y < h; y++ )
					{
						byte* s = src + y * stride;
						for( int x = 0; x < w; x++ )
						{
							byte b = s[ x * 4 + 0 ], g = s[ x * 4 + 1 ], r = s[ x * 4 + 2 ], a = s[ x * 4 + 3 ];
							if( halveAlpha )
								a = Tim2Util.Div2( a );
							if( imgType == Tim2ColorType.RGBA32 )
							{
								outBytes[ di++ ] = r;
								outBytes[ di++ ] = g;
								outBytes[ di++ ] = b;
								outBytes[ di++ ] = a;
							}
							else if( imgType == Tim2ColorType.RGB32 )
							{
								outBytes[ di++ ] = r;
								outBytes[ di++ ] = g;
								outBytes[ di++ ] = b;
							}
							else
							{
								ushort p = ToRgba5551Dithered( r, g, b, a, x, y );
								outBytes[ di++ ] = (byte)( p & 0xFF );
								outBytes[ di++ ] = (byte)( p >> 8 );
							}
						}
					}
				}
				return outBytes;
			}
			finally
			{
				argb.UnlockBits( data );
				if( !ReferenceEquals( argb, bmp ) )
					argb.Dispose();
			}
		}

		public static void ImportPngOverIndexed8(
			string tim2Path,
			string pngPath,
			string outTim2Path,
			int clutSet,
			bool dither,
			int kMeansIterations,
			bool serpentineDither,
			bool premultiplyForDither,
			bool writeSwizzledClut,
			bool halfAlpha )
		{
			Tim2Image t2;
			using( var fs = new FileStream( tim2Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ) )
			{
				t2 = Tim2Image.Load( fs );
			}

			if( t2.ImgType != Tim2ColorType.IDTEX8 )
				throw new NotSupportedException( $"TIM2 is {t2.ImgType}, expected IDTEX8." );

			using var bmpSrc = new Bitmap( pngPath );
			if( bmpSrc.Width != t2.Width || bmpSrc.Height != t2.Height )
				throw new InvalidDataException( $"PNG size {bmpSrc.Width}x{bmpSrc.Height} != TIM2 {t2.Width}x{t2.Height}" );

			if( t2.Pic.MipMapTexturesCount != 1 )
				throw new NotSupportedException( $"Only single-level TIM2 supported. MipMapTexturesCount={t2.Pic.MipMapTexturesCount}" );

			if( t2.Clut == null || t2.Pic.ClutColorsCount == 0 )
				throw new InvalidDataException( "Indexed TIM2 without CLUT." );

			const int perSet = 256;
			if( t2.Pic.ClutColorsCount % perSet != 0 )
				throw new InvalidDataException( $"Unexpected CLUT color count {t2.Pic.ClutColorsCount} for IDTEX8." );

			int setCount = t2.Pic.ClutColorsCount / perSet;
			if( clutSet < 0 || clutSet >= setCount )
				throw new ArgumentOutOfRangeException( nameof( clutSet ), $"Valid range: 0..{setCount - 1}" );

			byte[] newIndices;
			Rgba32[] palette;
			QuantizePngTo8bpp( bmpSrc, perSet, dither, kMeansIterations, out newIndices, out palette, serpentineDither, premultiplyForDither, t2.ClutType, IndexedExposureGain, IndexedBlackProtect );
			bool csm1 = Tim2Util.IsCsm1( t2.Pic.GsTex0 );
			bool doSwizzle = writeSwizzledClut && csm1;

			byte[] newClutBytes = doSwizzle
				? EncodePaletteToClutBytesSwizzled( palette, t2.ClutType, perSet, halveAlpha: halfAlpha && t2.ClutType == Tim2ColorType.RGBA32 )
				: EncodePaletteToClutBytes( palette, t2.ClutType, perSet, halveAlpha: halfAlpha && t2.ClutType == Tim2ColorType.RGBA32 );

			int bpp = Tim2Util.BytesPerClutColor( t2.ClutType );
			int setByteSize = perSet * bpp;
			int setOffsetBytes = clutSet * setByteSize;

			if( setOffsetBytes + setByteSize > t2.Clut.Length )
				throw new InvalidDataException( "CLUT buffer too small for computed set offset." );

			byte[] clutSpliced = new byte[ t2.Clut.Length ];
			Buffer.BlockCopy( t2.Clut, 0, clutSpliced, 0, t2.Clut.Length );
			Buffer.BlockCopy( newClutBytes, 0, clutSpliced, setOffsetBytes, setByteSize );
			t2.Pic.ImageDataSize = (uint)newIndices.Length;
			string tmp = AtomicIO.CreateTempSibling( outTim2Path );
			using var outFs = new FileStream( tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough );
			using var bw = new BinaryWriter( outFs );
			Tim2Util.WriteStruct( bw, t2.FileHeader );
			long pic0Off = ( t2.FileHeader.FormatId == 0x00 ) ? Marshal.SizeOf<TIM2_FILEHEADER>() : 0x80;

			if( outFs.Position < pic0Off )
				bw.Write( new byte[ pic0Off - outFs.Position ] );

			long phPos = outFs.Position;
			Tim2Util.WriteStruct( bw, t2.Pic );
			bw.Write( newIndices );
			int imgPad = Tim2Util.Align16( (int)t2.Pic.ImageDataSize ) - (int)t2.Pic.ImageDataSize;

			if( imgPad != 0 )
				bw.Write( new byte[ imgPad ] );

			if( t2.Pic.ClutDataSize > 0 )
			{
				bw.Write( clutSpliced );
				int clutPad = Tim2Util.Align16( (int)t2.Pic.ClutDataSize ) - (int)t2.Pic.ClutDataSize;
				if( clutPad != 0 )
					bw.Write( new byte[ clutPad ] );
			}

			long endPos = outFs.Position;
			t2.Pic.TotalSize = (uint)( endPos - phPos );
			outFs.Position = phPos;

			Tim2Util.WriteStruct( bw, t2.Pic );

			bw.Flush();
			outFs.Flush( true );
			outFs.Dispose();

			AtomicIO.ReplaceFile( tmp, outTim2Path );
		}

		public static void ImportPngOverIndexed4(
			string tim2Path,
			string pngPath,
			string outTim2Path,
			int clutSet,
			bool dither,
			int kMeansIterations,
			bool serpentineDither,
			bool premultiplyForDither,
			bool writeSwizzledClut,
			bool halfAlpha )
		{
			Tim2Image t2;
			using( var fs = new FileStream( tim2Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ) )
			{
				t2 = Tim2Image.Load( fs );
			}
			if( t2.ImgType != Tim2ColorType.IDTEX4 )
				throw new NotSupportedException( $"TIM2 is {t2.ImgType}, expected IDTEX4." );
			using var bmpSrc = new Bitmap( pngPath );
			if( bmpSrc.Width != t2.Width || bmpSrc.Height != t2.Height )
				throw new InvalidDataException( $"PNG size {bmpSrc.Width}x{bmpSrc.Height} != TIM2 {t2.Width}x{t2.Height}" );
			if( t2.Pic.MipMapTexturesCount != 1 )
				throw new NotSupportedException( $"Only single-level TIM2 supported. MipMapTexturesCount={t2.Pic.MipMapTexturesCount}" );
			if( t2.Clut == null || t2.Pic.ClutColorsCount == 0 )
				throw new InvalidDataException( "Indexed TIM2 (IDTEX4) without CLUT." );
			const int perSet = 16;
			if( t2.Pic.ClutColorsCount % perSet != 0 )
				throw new InvalidDataException( $"Unexpected CLUT color count {t2.Pic.ClutColorsCount} for IDTEX4." );
			int setCount = t2.Pic.ClutColorsCount / perSet;
			if( clutSet < 0 || clutSet >= setCount )
				throw new ArgumentOutOfRangeException( nameof( clutSet ), $"Valid range: 0..{setCount - 1}" );
			byte[] indices4;
			Rgba32[] palette;
			QuantizePngTo8bpp( bmpSrc, perSet, dither, kMeansIterations, out indices4, out palette, serpentineDither, premultiplyForDither, t2.ClutType, IndexedExposureGain, IndexedBlackProtect );
			int w = bmpSrc.Width, h = bmpSrc.Height;
			int pixCount = w * h;
			int packedLen = ( pixCount + 1 ) / 2;
			byte[] packed = new byte[ packedLen ];
			for( int i = 0, pi = 0; i < pixCount; i += 2, pi++ )
			{
				byte lo = (byte)( indices4[ i ] & 0x0F );
				byte hi = (byte)( ( i + 1 < pixCount ) ? ( indices4[ i + 1 ] & 0x0F ) : 0 );
				packed[ pi ] = (byte)( ( hi << 4 ) | lo );
			}
			byte[] newClutBytes = EncodePaletteToClutBytes( palette, t2.ClutType, perSet, halveAlpha: halfAlpha && t2.ClutType == Tim2ColorType.RGBA32 );
			int bpp = Tim2Util.BytesPerClutColor( t2.ClutType );
			int setByteSize = perSet * bpp;
			int setOffsetBytes = clutSet * setByteSize;
			if( setOffsetBytes + setByteSize > t2.Clut.Length )
				throw new InvalidDataException( "CLUT buffer too small for computed set offset." );
			byte[] clutSpliced = new byte[ t2.Clut.Length ];
			Buffer.BlockCopy( t2.Clut, 0, clutSpliced, 0, t2.Clut.Length );
			Buffer.BlockCopy( newClutBytes, 0, clutSpliced, setOffsetBytes, setByteSize );
			t2.Pic.ImageDataSize = (uint)packed.Length;
			string tmp = AtomicIO.CreateTempSibling( outTim2Path );
			using var outFs = new FileStream( tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough );
			using var bw = new BinaryWriter( outFs );
			Tim2Util.WriteStruct( bw, t2.FileHeader );
			long pic0Off = ( t2.FileHeader.FormatId == 0x00 ) ? Marshal.SizeOf<TIM2_FILEHEADER>() : 0x80;
			if( outFs.Position < pic0Off )
				bw.Write( new byte[ pic0Off - outFs.Position ] );
			long phPos = outFs.Position;
			Tim2Util.WriteStruct( bw, t2.Pic );
			bw.Write( packed );
			int imgPad = Tim2Util.Align16( (int)t2.Pic.ImageDataSize ) - (int)t2.Pic.ImageDataSize;
			if( imgPad != 0 )
				bw.Write( new byte[ imgPad ] );
			if( t2.Pic.ClutDataSize > 0 )
			{
				bw.Write( clutSpliced );
				int clutPad = Tim2Util.Align16( (int)t2.Pic.ClutDataSize ) - (int)t2.Pic.ClutDataSize;
				if( clutPad != 0 )
					bw.Write( new byte[ clutPad ] );
			}
			long endPos = outFs.Position;
			t2.Pic.TotalSize = (uint)( endPos - phPos );
			outFs.Position = phPos;
			Tim2Util.WriteStruct( bw, t2.Pic );
			bw.Flush();
			outFs.Flush( true );
			outFs.Dispose();
			AtomicIO.ReplaceFile( tmp, outTim2Path );
		}

		private static byte[] EncodePaletteToClutBytes( IReadOnlyList<Rgba32> pal, Tim2ColorType clutType, int perSet, bool halveAlpha )
		{
			int count = Math.Min( perSet, pal.Count );
			int bpp = Tim2Util.BytesPerClutColor( clutType );
			var bytes = new byte[ perSet * bpp ];
			var last = pal[ count > 0 ? count - 1 : 0 ];
			for( int i = 0; i < perSet; i++ )
			{
				var c = i < count ? pal[ i ] : last;
				int ofs = i * bpp;
				switch( clutType )
				{
					case Tim2ColorType.RGB32:
					bytes[ ofs + 0 ] = c.R;
					bytes[ ofs + 1 ] = c.G;
					bytes[ ofs + 2 ] = c.B;
					break;
					case Tim2ColorType.RGBA32:
					bytes[ ofs + 0 ] = c.R;
					bytes[ ofs + 1 ] = c.G;
					bytes[ ofs + 2 ] = c.B;
					bytes[ ofs + 3 ] = halveAlpha ? Tim2Util.Div2( c.A ) : c.A;
					break;
					case Tim2ColorType.RGBA16:
					{
						ushort pr = (ushort)( c.R >> 3 );
						ushort pg = (ushort)( c.G >> 3 );
						ushort pb = (ushort)( c.B >> 3 );
						ushort pa = (ushort)( c.A >= 0x80 ? 1 : 0 );
						ushort p = (ushort)( ( pa << 15 ) | ( pb << 10 ) | ( pg << 5 ) | pr );
						bytes[ ofs + 0 ] = (byte)( p & 0xFF );
						bytes[ ofs + 1 ] = (byte)( p >> 8 );
						break;
					}
				}
			}
			return bytes;
		}

		private static byte[] EncodePaletteToClutBytesSwizzled( IReadOnlyList<Rgba32> pal, Tim2ColorType clutType, int perSet, bool halveAlpha )
		{
			int count = Math.Min( perSet, pal.Count );
			int bpp = Tim2Util.BytesPerClutColor( clutType );
			var bytes = new byte[ perSet * bpp ];
			var last = pal[ count > 0 ? count - 1 : 0 ];
			for( int i = 0; i < perSet; i++ )
			{
				var c = i < count ? pal[ i ] : last;
				int wi = Tim2Util.RemapClutIndexWrite( i, perSet );
				int ofs = wi * bpp;
				switch( clutType )
				{
					case Tim2ColorType.RGB32:
					bytes[ ofs + 0 ] = c.R;
					bytes[ ofs + 1 ] = c.G;
					bytes[ ofs + 2 ] = c.B;
					break;
					case Tim2ColorType.RGBA32:
					bytes[ ofs + 0 ] = c.R;
					bytes[ ofs + 1 ] = c.G;
					bytes[ ofs + 2 ] = c.B;
					bytes[ ofs + 3 ] = halveAlpha ? Tim2Util.Div2( c.A ) : c.A;
					break;
					case Tim2ColorType.RGBA16:
					{
						ushort pr = (ushort)( c.R >> 3 );
						ushort pg = (ushort)( c.G >> 3 );
						ushort pb = (ushort)( c.B >> 3 );
						ushort pa = (ushort)( c.A >= 0x80 ? 1 : 0 );
						ushort p = (ushort)( ( pa << 15 ) | ( pb << 10 ) | ( pg << 5 ) | pr );
						bytes[ ofs + 0 ] = (byte)( p & 0xFF );
						bytes[ ofs + 1 ] = (byte)( p >> 8 );
						break;
					}
				}
			}
			return bytes;
		}

		private static void QuantizePngTo8bpp(
			Bitmap src,
			int maxColors,
			bool dither,
			int kMeansIterations,
			out byte[] indices,
			out Rgba32[] palette,
			bool serpentineDither,
			bool premultiplyForDither,
			Tim2ColorType clutType,
			float exposureGain,
			float blackProtect )
		{
			var rect = new Rectangle( 0, 0, src.Width, src.Height );
			var bmp = src.PixelFormat == PixelFormat.Format32bppArgb ? src : src.Clone( rect, PixelFormat.Format32bppArgb );
			var data = bmp.LockBits( rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb );

			try
			{
				unsafe
				{
					byte* basePtr = (byte*)data.Scan0;
					int stride = data.Stride;
					int w = bmp.Width, h = bmp.Height;
					var samples = new List<Rgba32>( Math.Min( w * h, 40000 ) );
					int stepX = Math.Max( 1, w / 200 );
					int stepY = Math.Max( 1, h / 200 );

					for( int y = 0; y < h; y += stepY )
					{
						byte* row = basePtr + y * stride;
						for( int x = 0; x < w; x += stepX )
						{
							byte b = row[ x * 4 + 0 ], g = row[ x * 4 + 1 ], r = row[ x * 4 + 2 ], a = row[ x * 4 + 3 ];
							if( premultiplyForDither )
							{ r = (byte)( r * a / 255 ); g = (byte)( g * a / 255 ); b = (byte)( b * a / 255 ); }
							samples.Add( new Rgba32( r, g, b, a ) );
						}
					}

					palette = KMeansPalette( samples, Math.Max( 1, Math.Min( 256, maxColors ) ), kMeansIterations );
					indices = new byte[ w * h ];

					if( !dither )
					{
						for( int y = 0, di = 0; y < h; y++ )
						{
							byte* row = basePtr + y * stride;
							for( int x = 0; x < w; x++, di++ )
							{
								byte b = row[ x * 4 + 0 ], g = row[ x * 4 + 1 ], r = row[ x * 4 + 2 ], a = row[ x * 4 + 3 ];
								if( premultiplyForDither )
								{ r = (byte)( r * a / 255 ); g = (byte)( g * a / 255 ); b = (byte)( b * a / 255 ); }
								indices[ di ] = (byte)FindNearest( palette, r, g, b, a, clutType, exposureGain, blackProtect );
							}
						}
					}
					else
					{
						var work = new float[ w * h * 4 ];

						for( int y = 0; y < h; y++ )
						{
							byte* row = basePtr + y * stride;
							for( int x = 0; x < w; x++ )
							{
								int o = ( y * w + x ) * 4;
								byte r = row[ x * 4 + 2 ], g = row[ x * 4 + 1 ], b = row[ x * 4 + 0 ], a = row[ x * 4 + 3 ];
								if( premultiplyForDither )
								{ r = (byte)( r * a / 255 ); g = (byte)( g * a / 255 ); b = (byte)( b * a / 255 ); }
								work[ o + 0 ] = r;
								work[ o + 1 ] = g;
								work[ o + 2 ] = b;
								work[ o + 3 ] = a;
							}
						}

						for( int y = 0; y < h; y++ )
						{
							bool leftToRight = !serpentineDither || ( y % 2 == 0 );
							int xStart = leftToRight ? 0 : w - 1;
							int xEnd = leftToRight ? w : -1;
							int xStep = leftToRight ? 1 : -1;

							for( int x = xStart; x != xEnd; x += xStep )
							{
								int o = ( y * w + x ) * 4;
								byte r0 = ClampToByte( work[ o + 0 ] );
								byte g0 = ClampToByte( work[ o + 1 ] );
								byte b0 = ClampToByte( work[ o + 2 ] );
								byte a0 = ClampToByte( work[ o + 3 ] );
								int idx = FindNearest( palette, r0, g0, b0, a0, clutType, exposureGain, blackProtect );
								indices[ y * w + x ] = (byte)idx;
								var pc = palette[ idx ];
								float er = r0 - pc.R, eg = g0 - pc.G, eb = b0 - pc.B;

								if( leftToRight )
								{
									Diffuse( work, w, h, x + 1, y + 0, er, eg, eb, 7f / 16f );
									Diffuse( work, w, h, x - 1, y + 1, er, eg, eb, 3f / 16f );
									Diffuse( work, w, h, x + 0, y + 1, er, eg, eb, 5f / 16f );
									Diffuse( work, w, h, x + 1, y + 1, er, eg, eb, 1f / 16f );
								}
								else
								{
									Diffuse( work, w, h, x - 1, y + 0, er, eg, eb, 7f / 16f );
									Diffuse( work, w, h, x + 1, y + 1, er, eg, eb, 3f / 16f );
									Diffuse( work, w, h, x + 0, y + 1, er, eg, eb, 5f / 16f );
									Diffuse( work, w, h, x - 1, y + 1, er, eg, eb, 1f / 16f );
								}
							}
						}
					}
				}
			}
			finally
			{
				bmp.UnlockBits( data );
				if( !ReferenceEquals( bmp, src ) )
					bmp.Dispose();
			}
		}

		private static void Diffuse( float[] buf, int w, int h, int x, int y, float er, float eg, float eb, float k )
		{
			if( (uint)x >= (uint)w || (uint)y >= (uint)h )
				return;
			int o = ( y * w + x ) * 4;
			buf[ o + 0 ] = ClampToByteF( buf[ o + 0 ] + er * k );
			buf[ o + 1 ] = ClampToByteF( buf[ o + 1 ] + eg * k );
			buf[ o + 2 ] = ClampToByteF( buf[ o + 2 ] + eb * k );
		}

		private static byte ClampToByte( float v ) => (byte)( v < 0 ? 0 : v > 255 ? 255 : v );
		private static float ClampToByteF( float v ) => v < 0 ? 0 : ( v > 255 ? 255 : v );

		private static int FindNearest( IReadOnlyList<Rgba32> pal, byte r, byte g, byte b, byte a, Tim2ColorType clutType, float exposureGain, float blackProtect )
		{
			float lr = SrgbToLinear( r ), lg = SrgbToLinear( g ), lb = SrgbToLinear( b );

			if( exposureGain != 1f )
				ApplyGainWithBlackProtect( ref lr, ref lg, ref lb, exposureGain, blackProtect );

			float aw = clutType switch
			{
				Tim2ColorType.RGBA32 => 1.0f,
				Tim2ColorType.RGBA16 => 0.35f,
				Tim2ColorType.RGB32 => 0.0f,
				_ => 0.0f
			};

			int best = 0;
			double bestD = double.MaxValue;

			for( int i = 0; i < pal.Count; i++ )
			{
				var p = pal[ i ];
				float pr = SrgbToLinear( p.R ), pg = SrgbToLinear( p.G ), pb = SrgbToLinear( p.B );
				float dr = lr - pr, dg = lg - pg, db = lb - pb;
				double d = dr * dr * 0.8 + dg * dg + db * db * 0.9;
				if( aw > 0f )
				{ float da = ( a - p.A ) / 255f; d += aw * 0.5 * da * da; }
				if( d < bestD )
				{ bestD = d; best = i; }
			}
			return best;
		}

		private static Rgba32[] KMeansPalette( List<Rgba32> samples, int maxColors, int iters )
		{
			maxColors = Math.Max( 1, Math.Min( 256, maxColors ) );

			if( samples.Count == 0 )
				return new[] { new Rgba32( 0, 0, 0, 0 ) };

			if( samples.Count <= maxColors )
			{
				var set = new Dictionary<uint, Rgba32>( samples.Count );

				foreach( var s in samples )
				{
					uint key = (uint)( s.A << 24 | s.R << 16 | s.G << 8 | s.B );
					if( !set.ContainsKey( key ) )
						set[ key ] = s;
					if( set.Count >= maxColors )
						break;
				}

				var outArr = new Rgba32[ set.Count ];
				int i = 0;

				foreach( var kv in set )
					outArr[ i++ ] = kv.Value;

				return outArr;
			}

			var rnd = new Random( 1234 );
			var centers = new List<Rgba32>( maxColors );
			centers.Add( samples[ rnd.Next( samples.Count ) ] );

			while( centers.Count < maxColors )
			{
				var dist = new double[ samples.Count ];
				double sum = 0;
				for( int i = 0; i < samples.Count; i++ )
				{
					var s = samples[ i ];
					double best = double.MaxValue;
					foreach( var c in centers )
					{
						int dr = s.R - c.R, dg = s.G - c.G, db = s.B - c.B, da = s.A - c.A;
						double d = dr * dr * 0.8 + dg * dg + db * db * 0.9 + 0.2 * da * da;
						if( d < best )
							best = d;
					}
					dist[ i ] = best;
					sum += best;
				}
				double rpick = rnd.NextDouble() * sum;
				for( int i = 0; i < samples.Count; i++ )
				{ rpick -= dist[ i ]; if( rpick <= 0 ) { centers.Add( samples[ i ] ); break; } }
			}

			var sumR = new int[ maxColors ];
			var sumG = new int[ maxColors ];
			var sumB = new int[ maxColors ];
			var sumA = new int[ maxColors ];
			var cnt = new int[ maxColors ];

			for( int it = 0; it < iters; it++ )
			{
				Array.Clear( sumR, 0, maxColors );
				Array.Clear( sumG, 0, maxColors );
				Array.Clear( sumB, 0, maxColors );
				Array.Clear( sumA, 0, maxColors );
				Array.Clear( cnt, 0, maxColors );

				foreach( var s in samples )
				{
					int best = 0;
					long bestD = long.MaxValue;
					for( int k = 0; k < centers.Count; k++ )
					{
						var c = centers[ k ];
						long dr = s.R - c.R;
						dr *= dr;
						long dg = s.G - c.G;
						dg *= dg;
						long db = s.B - c.B;
						db *= db;
						long da = s.A - c.A;
						da *= da;
						long d = (long)( dr * 0.8 + dg + db * 0.9 + da * 0.2 );
						if( d < bestD )
						{ bestD = d; best = k; }
					}
					sumR[ best ] += s.R;
					sumG[ best ] += s.G;
					sumB[ best ] += s.B;
					sumA[ best ] += s.A;
					cnt[ best ]++;
				}

				for( int k = 0; k < centers.Count; k++ )
				{
					if( cnt[ k ] == 0 )
						continue;
					centers[ k ] = new Rgba32( (byte)( sumR[ k ] / cnt[ k ] ), (byte)( sumG[ k ] / cnt[ k ] ), (byte)( sumB[ k ] / cnt[ k ] ), (byte)( sumA[ k ] / cnt[ k ] ) );
				}
			}

			var uniq = new Dictionary<uint, Rgba32>( centers.Count );
			foreach( var c in centers )
			{
				uint key = (uint)( c.A << 24 | c.R << 16 | c.G << 8 | c.B );
				if( !uniq.ContainsKey( key ) )
					uniq[ key ] = c;
			}

			var pal = new List<Rgba32>( uniq.Values );

			if( pal.Count > maxColors )
				pal.RemoveRange( maxColors, pal.Count - maxColors );

			return pal.ToArray();
		}
	}
}
