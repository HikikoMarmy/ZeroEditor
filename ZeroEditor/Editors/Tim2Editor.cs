using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ZeroEditor.Tim2;

namespace ZeroEditor.Common
{
	public partial class Tim2Editor : UserControl
	{
		private string? _path;
		private Tim2Image? _tim2;
		private int _clutSet = 0;
		private readonly ToolTip _tip = new ToolTip();
		private bool _halfAlpha = true;

		public Tim2Editor()
		{
			InitializeComponent();
			ThemeManager.ApplyDark( this );
			pictureBox.BackColor = Color.Black;
			pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
			btnExport.Click += btnExport_Click;
			btnImport.Click += btnImport_Click;
			chkHalfAlpha.CheckedChanged += ( s, e ) => { _halfAlpha = chkHalfAlpha.Checked; RenderPreview(); RenderInfo(); };
			chkHalfAlpha.Checked = true;
			SetStyle( ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true );
			Disposed += ( _, __ ) =>
			{
				try
				{ _tip?.Dispose(); }
				catch { }
				if( pictureBox.Image is not null )
				{
					pictureBox.Image.Dispose();
					pictureBox.Image = null;
				}
			};
		}

		public void LoadTim2( string path, int clutSet = 0 )
		{
			var data = File.ReadAllBytes( path );
			LoadTim2Bytes( data, clutSet, path );
		}

		public void LoadTim2Bytes( byte[] data, int clutSet = 0, string? sourcePath = null )
		{
			using var ms = new MemoryStream( data, writable: false );
			var t2 = Tim2Image.Load( ms );
			_path = sourcePath ?? _path;
			_clutSet = clutSet;
			_tim2 = t2;
			RenderPreview();
			RenderInfo();
		}

		private void RenderPreview()
		{
			if( _tim2 is null )
				return;
			using var bmp = Tim2Decode.DecodeToBitmap( _tim2, _clutSet, _halfAlpha );
			var clone = (Bitmap)bmp.Clone();
			var old = pictureBox.Image;
			pictureBox.Image = clone;
			old?.Dispose();
			var type = _tim2.ImgType.ToString();
			var clut = _tim2.Clut is null ? "" : $"/{_tim2.ClutType}";
			var label = $"{Path.GetFileName( _path )} — {_tim2.Width}×{_tim2.Height} {type}{clut}";
			pictureBox.Tag = label;
			_tip.SetToolTip( pictureBox, label );
		}

		private int BitsPerPixel( Tim2ColorType t )
		{
			switch( t )
			{
				case Tim2ColorType.RGBA16:
				return 16;
				case Tim2ColorType.RGB32:
				return 24;
				case Tim2ColorType.RGBA32:
				return 32;
				case Tim2ColorType.IDTEX8:
				return 8;
				case Tim2ColorType.IDTEX4:
				return 4;
				default:
				return 0;
			}
		}

		private int BitsPerClutEntry( Tim2ColorType t )
		{
			switch( t )
			{
				case Tim2ColorType.RGBA16:
				return 16;
				case Tim2ColorType.RGB32:
				return 24;
				case Tim2ColorType.RGBA32:
				return 32;
				default:
				return 0;
			}
		}

		private string BuildInfoText()
		{
			if( _tim2 == null )
				return "";
			var sb = new StringBuilder();
			var imgType = _tim2.ImgType;
			var clutType = _tim2.ClutType;
			int bpp = BitsPerPixel( imgType );
			int clutBpp = BitsPerClutEntry( clutType );
			int clutColors = _tim2.Pic.ClutColorsCount;
			bool hasClut = _tim2.Clut != null && clutColors > 0;
			int perSet = imgType == Tim2ColorType.IDTEX4 ? 16 : imgType == Tim2ColorType.IDTEX8 ? 256 : 0;
			int setCount = hasClut && perSet > 0 ? Math.Max( 1, clutColors / perSet ) : 0;
			bool csm1 = Tim2Util.IsCsm1( _tim2.Pic.GsTex0 );
			sb.AppendLine( $"File: {Path.GetFileName( _path )}" );
			sb.AppendLine( $"Resolution: {_tim2.Width} × {_tim2.Height}" );
			sb.AppendLine( $"Type: {imgType} ({bpp} bpp)" );
			sb.AppendLine( $"Mipmaps: {_tim2.Pic.MipMapTexturesCount}" );
			sb.AppendLine( $"GS CSM: {( csm1 ? "CSM1" : "CSM2" )}" );
			if( hasClut )
			{
				sb.AppendLine( $"CLUT: {clutType} ({clutBpp} bpp), Colors: {clutColors}" );
				if( setCount > 0 )
					sb.AppendLine( $"CLUT Sets: {setCount}, Current Set: {_clutSet}" );
			}
			sb.AppendLine( $"Half alpha: {( _halfAlpha ? "On" : "Off" )}" );
			return sb.ToString();
		}

		private void RenderInfo()
		{
			lblInfo.Text = BuildInfoText();
		}

		private void btnExport_Click( object? sender, EventArgs e )
		{
			try
			{
				if( _path is null )
					return;
				using var sfd = new SaveFileDialog
				{
					Filter = "PNG|*.png",
					FileName = Path.GetFileNameWithoutExtension( _path ) + ".png"
				};
				if( sfd.ShowDialog( this ) != DialogResult.OK )
					return;
				using var fs = File.OpenRead( _path );
				var t2 = Tim2Image.Load( fs );
				using var bmp = Tim2Decode.DecodeToBitmap( t2, _clutSet, _halfAlpha );
				bmp.Save( sfd.FileName, System.Drawing.Imaging.ImageFormat.Png );
				MessageBox.Show( this, "Exported PNG successfully.", "TIM2 Export" );
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Export failed" );
			}
		}

		private void btnImport_Click( object? sender, EventArgs e )
		{
			try
			{
				if( _path is null || _tim2 is null )
					return;
				if( _tim2.Image.Length == 0 )
				{
					MessageBox.Show( this, "This file is CLUT-only (CLT2) and has no image payload to replace.", "Import not supported", MessageBoxButtons.OK, MessageBoxIcon.Information );
					return;
				}
				using var ofd = new OpenFileDialog { Filter = "PNG|*.png", Title = "Choose PNG to import" };
				if( ofd.ShowDialog( this ) != DialogResult.OK )
					return;
				using var sfd = new SaveFileDialog
				{
					Filter = "TIM2|*.tm2;*.cl2|All files|*.*",
					FileName = Path.GetFileName( _path ),
					InitialDirectory = Path.GetDirectoryName( _path )
				};

				if( sfd.ShowDialog( this ) != DialogResult.OK )
					return;

				Tim2Encode.ImportPngAuto(
					tim2Path: _path,
					pngPath: ofd.FileName,
					outTim2Path: sfd.FileName,
					clutSet: _clutSet,
					dither: true,
					kMeansIterations: 16,
					serpentineDither: true,
					premultiplyForDither: false,
					writeSwizzledClut: true,
					halfAlpha: _halfAlpha
				);

				LoadTim2( sfd.FileName, _clutSet );
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Import failed" );
			}
		}
	}
}
