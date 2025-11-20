using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ZeroEditor.Tim2
{
	public static class Tim2Decode
	{
		private static uint Rgba16ToRgba32( ushort p )
		{
			byte r = (byte)( ( p << 3 ) & 0xF8 );
			byte g = (byte)( ( p >> 2 ) & 0xF8 );
			byte b = (byte)( ( p >> 7 ) & 0xF8 );
			byte a = (byte)( ( p >> 8 ) & 0x80 );
			return (uint)( a << 24 | b << 16 | g << 8 | r );
		}

		private static unsafe void SetPixel( byte* dst, int idx, uint c )
		{
			dst[ idx + 0 ] = (byte)( ( c >> 16 ) & 0xFF );
			dst[ idx + 1 ] = (byte)( ( c >> 8 ) & 0xFF );
			dst[ idx + 2 ] = (byte)( c & 0xFF );
			dst[ idx + 3 ] = (byte)( ( c >> 24 ) & 0xFF );
		}

		private static uint GetClutColor(
			byte[] clut,
			Tim2ColorType clutType,
			Tim2ColorType imgType,
			int headerClutColorsCount,
			int clutSet,
			int palIndex,
			bool csm1,
			bool halfAlpha )
		{
			int perSet = imgType switch
			{
				Tim2ColorType.IDTEX4 => 16,
				Tim2ColorType.IDTEX8 => 256,
				_ => 0
			};
			if( perSet == 0 )
				return 0;

			int num = clutSet * perSet + palIndex;
			if( num < 0 )
				return 0;

			int bpp = Tim2Util.BytesPerClutColor( clutType );
			if( bpp <= 0 || clut.Length < bpp )
				return 0;

			int maxColorsByData = clut.Length / bpp;
			int effectiveColors = Math.Min( headerClutColorsCount, maxColorsByData );
			if( num >= effectiveColors )
				return 0;

			int sw = ( csm1 && imgType == Tim2ColorType.IDTEX8 ) ? Tim2Util.RemapClutIndex32Bank( num ) : num;
			if( sw < 0 || sw >= effectiveColors )
				return 0;

			int ofs = sw * bpp;
			switch( clutType )
			{
				case Tim2ColorType.RGBA16:
				{
					if( ofs + 1 >= clut.Length )
						return 0;
					ushort p = (ushort)( ( clut[ ofs + 1 ] << 8 ) | clut[ ofs + 0 ] );
					return Rgba16ToRgba32( p );
				}
				case Tim2ColorType.RGB32:
				{
					if( ofs + 2 >= clut.Length )
						return 0;
					byte r = clut[ ofs + 0 ], g = clut[ ofs + 1 ], b = clut[ ofs + 2 ];
					return (uint)( 0x80 << 24 | b << 16 | g << 8 | r );
				}
				case Tim2ColorType.RGBA32:
				{
					if( ofs + 3 >= clut.Length )
						return 0;
					byte r = clut[ ofs + 0 ], g = clut[ ofs + 1 ], b = clut[ ofs + 2 ], a = clut[ ofs + 3 ];
					if( halfAlpha )
						a = Tim2Util.Mul2Clamp( a );
					return (uint)( a << 24 | b << 16 | g << 8 | r );
				}
			}
			return 0;
		}

		public static unsafe Bitmap DecodeToBitmap( Tim2Image tim2, int clutSet, bool halfAlpha )
		{
			if( tim2 is null )
				throw new ArgumentNullException( nameof( tim2 ) );

			if( tim2.Image.Length == 0 )
			{
				if( tim2.Clut is null || tim2.Pic.ClutColorsCount == 0 )
					throw new InvalidDataException( "TIM2 has neither image nor CLUT data." );
				return RenderClutPreview( tim2, halfAlpha );
			}

			int w = tim2.Width, h = tim2.Height;
			var bmp = new Bitmap( w, h, PixelFormat.Format32bppArgb );
			var bmpData = bmp.LockBits( new Rectangle( 0, 0, w, h ), ImageLockMode.WriteOnly, bmp.PixelFormat );
			bool csm1 = Tim2Util.IsCsm1( tim2.Pic.GsTex0 );

			try
			{
				byte* dst = (byte*)bmpData.Scan0;
				int stride = bmpData.Stride;
				var imgType = tim2.ImgType;
				var clutType = tim2.ClutType;
				var img = tim2.Image;
				var clut = tim2.Clut;
				int expectBytes = imgType switch
				{
					Tim2ColorType.RGBA16 => checked(w * h * 2),
					Tim2ColorType.RGB32 => checked(w * h * 3),
					Tim2ColorType.RGBA32 => checked(w * h * 4),
					Tim2ColorType.IDTEX8 => checked(w * h),
					Tim2ColorType.IDTEX4 => ( checked(w * h) + 1 ) / 2,
					_ => 0
				};

				if( img.Length < expectBytes )
					throw new InvalidDataException( $"TIM2 image payload too short: have {img.Length}, need {expectBytes}." );

				if( imgType == Tim2ColorType.RGBA16 )
				{
					for( int y = 0, si = 0; y < h; y++ )
					{
						byte* row = dst + y * stride;
						for( int x = 0; x < w; x++, si += 2 )
						{
							ushort p = (ushort)( img[ si + 0 ] | ( img[ si + 1 ] << 8 ) );
							SetPixel( row, x * 4, Rgba16ToRgba32( p ) );
						}
					}
				}
				else if( imgType == Tim2ColorType.RGB32 )
				{
					for( int y = 0, si = 0; y < h; y++ )
					{
						byte* row = dst + y * stride;
						for( int x = 0; x < w; x++, si += 3 )
						{
							byte r = img[ si + 0 ], g = img[ si + 1 ], b = img[ si + 2 ];
							SetPixel( row, x * 4, (uint)( 0x80 << 24 | b << 16 | g << 8 | r ) );
						}
					}
				}
				else if( imgType == Tim2ColorType.RGBA32 )
				{
					for( int y = 0, si = 0; y < h; y++ )
					{
						byte* row = dst + y * stride;
						for( int x = 0; x < w; x++, si += 4 )
						{
							byte r = img[ si + 0 ], g = img[ si + 1 ], b = img[ si + 2 ], a = img[ si + 3 ];
							if( halfAlpha )
								a = Tim2Util.Mul2Clamp( a );
							SetPixel( row, x * 4, (uint)( a << 24 | b << 16 | g << 8 | r ) );
						}
					}
				}
				else if( imgType == Tim2ColorType.IDTEX8 )
				{
					if( clut is null )
						throw new InvalidDataException( "IDTEX8 without CLUT." );
					int colors = Math.Min( tim2.Pic.ClutColorsCount, clut.Length / Math.Max( 1, Tim2Util.BytesPerClutColor( clutType ) ) );
					for( int y = 0, si = 0; y < h; y++ )
					{
						byte* row = dst + y * stride;
						for( int x = 0; x < w; x++, si++ )
						{
							int idx = img[ si ];
							uint c = GetClutColor( clut, clutType, imgType, colors, clutSet, idx, csm1, halfAlpha );
							SetPixel( row, x * 4, c );
						}
					}
				}
				else if( imgType == Tim2ColorType.IDTEX4 )
				{
					if( clut is null )
						throw new InvalidDataException( "IDTEX4 without CLUT." );
					int colors = Math.Min( tim2.Pic.ClutColorsCount, clut.Length / Math.Max( 1, Tim2Util.BytesPerClutColor( clutType ) ) );
					for( int y = 0, si = 0; y < h; y++ )
					{
						byte* row = dst + y * stride;
						for( int x = 0; x < w; x += 2, si++ )
						{
							int packed = img[ si ];
							int idx0 = packed & 0x0F;
							int idx1 = ( packed >> 4 ) & 0x0F;
							uint c0 = GetClutColor( clut, clutType, imgType, colors, clutSet, idx0, csm1, halfAlpha );
							SetPixel( row, x * 4, c0 );
							if( x + 1 < w )
							{
								uint c1 = GetClutColor( clut, clutType, imgType, colors, clutSet, idx1, csm1, halfAlpha );
								SetPixel( row, ( x + 1 ) * 4, c1 );
							}
						}
					}
				}
				else
				{
					throw new NotSupportedException( $"Unsupported TIM2 image type: {imgType}" );
				}
			}
			finally
			{
				bmp.UnlockBits( bmpData );
			}
			return bmp;
		}

		private static Bitmap RenderClutPreview( Tim2Image tim2, bool halfAlpha )
		{
			int bpp = Tim2Util.BytesPerClutColor( tim2.ClutType );
			int maxByData = tim2.Clut!.Length / Math.Max( bpp, 1 );
			int colors = Math.Min( tim2.Pic.ClutColorsCount, maxByData );

			if( colors <= 0 )
				throw new InvalidDataException( "TIM2 CLUT present but empty/invalid." );

			int cols = colors >= 256 ? 16 : ( colors == 16 ? 8 : 16 );
			int rows = ( colors + cols - 1 ) / cols;
			const int cell = 16;
			var bmp = new Bitmap( cols * cell, rows * cell, PixelFormat.Format32bppArgb );
			var data = bmp.LockBits( new Rectangle( 0, 0, bmp.Width, bmp.Height ), ImageLockMode.WriteOnly, bmp.PixelFormat );
			bool csm1 = Tim2Util.IsCsm1( tim2.Pic.GsTex0 );

			try
			{
				unsafe
				{
					byte* basePtr = (byte*)data.Scan0;

					for( int i = 0; i < colors; i++ )
					{
						int cx = i % cols, cy = i / cols;
						uint c = GetClutColor( tim2.Clut!, tim2.ClutType, Tim2ColorType.IDTEX8, colors, 0, i, csm1, halfAlpha );
						for( int y = 0; y < cell; y++ )
						{
							byte* row = basePtr + ( cy * cell + y ) * data.Stride + cx * cell * 4;
							for( int x = 0; x < cell; x++ )
								SetPixel( row, x * 4, c );
						}
					}
				}
			}
			finally
			{
				bmp.UnlockBits( data );
			}
			return bmp;
		}
	}
}
