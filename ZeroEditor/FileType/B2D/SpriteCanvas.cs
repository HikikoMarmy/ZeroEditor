using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ZeroEditor.Zero3.Editors.B2D
{
	public sealed class SpriteCanvas : Control
	{
		B2dFile? _model;
		ITextureResolver? _resolver;
		public float Zoom { get; set; } = 1f;
		public PointF Pan { get; set; } = new PointF( 0, 0 );
		public int? FilterTextureId { get { return _filterId; } set { _filterId = value; Invalidate(); } }
		int? _filterId;
		public B2dSprite? SelectedSprite { get; set; }
		public event EventHandler? SelectionChanged;
		bool _panning;
		Point _last;
		PointF _worldCenter;
		string? _fallbackTex;

		public SpriteCanvas()
		{
			DoubleBuffered = true;
			SetStyle( ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true );
			MouseWheel += SpriteCanvas_MouseWheel;
			MouseDown += ( s, e ) =>
			{
				if( e.Button == MouseButtons.Right )
				{ _panning = true; _last = e.Location; Cursor = Cursors.SizeAll; }
				else if( e.Button == MouseButtons.Left )
					HitTest( e.Location );
			};
			MouseUp += ( s, e ) => { if( e.Button == MouseButtons.Right ) { _panning = false; Cursor = Cursors.Default; } };
			MouseMove += ( s, e ) =>
			{
				if( _panning )
				{ Pan = new PointF( Pan.X + ( e.X - _last.X ), Pan.Y + ( e.Y - _last.Y ) ); _last = e.Location; Invalidate(); }
			};
		}

		public void SetModel( B2dFile model, ITextureResolver resolver )
		{
			_model = model;
			_resolver = resolver;
			ComputeBoundsAndCenter();
			Zoom = 1f;
			Pan = new PointF( 0, 0 );
			Invalidate();
		}

		public void Fit()
		{
			Zoom = 1f;
			Pan = new PointF( 0, 0 );
			Invalidate();
		}

		void ComputeBoundsAndCenter()
		{
			_worldCenter = new PointF( 0, 0 );
			_fallbackTex = null;
			if( _model == null )
				return;
			if( _model.SpritesInOrder.Count == 0 )
				return;
			int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
			foreach( var sp in _model.SpritesInOrder )
			{
				if( sp.SrcRect.X < minX )
					minX = sp.SrcRect.X;
				if( sp.SrcRect.Y < minY )
					minY = sp.SrcRect.Y;
				int rx = sp.SrcRect.Right;
				int by = sp.SrcRect.Bottom;
				if( rx > maxX )
					maxX = rx;
				if( by > maxY )
					maxY = by;
			}
			if( minX == int.MaxValue || minY == int.MaxValue )
				return;
			_worldCenter = new PointF( 0.5f * ( minX + maxX ), 0.5f * ( minY + maxY ) );
			string? pick = null;
			foreach( var s in _model.StringTable )
			{
				if( string.IsNullOrEmpty( s ) )
					continue;
				if( s.Equals( "_RegistSprite_", StringComparison.OrdinalIgnoreCase ) )
					continue;
				if( s.StartsWith( "cm_", StringComparison.OrdinalIgnoreCase ) )
				{ pick = s; break; }
			}
			if( pick == null )
			{
				foreach( var s in _model.StringTable )
				{
					if( string.IsNullOrEmpty( s ) )
						continue;
					if( s.Equals( "_RegistSprite_", StringComparison.OrdinalIgnoreCase ) )
						continue;
					pick = s;
					break;
				}
			}
			_fallbackTex = pick;
		}

		void SpriteCanvas_MouseWheel( object? sender, MouseEventArgs e )
		{
			float factor = e.Delta > 0 ? 1.1f : 1f / 1.1f;
			Zoom *= factor;
			Invalidate();
		}

		protected override void OnPaint( PaintEventArgs e )
		{
			base.OnPaint( e );
			e.Graphics.Clear( BackColor );
			if( _model == null || _resolver == null )
				return;
			var canvasCenter = new PointF( ClientSize.Width * 0.5f + Pan.X, ClientSize.Height * 0.5f + Pan.Y );
			e.Graphics.TranslateTransform( canvasCenter.X, canvasCenter.Y );
			e.Graphics.ScaleTransform( Zoom, Zoom );
			e.Graphics.TranslateTransform( -_worldCenter.X, -_worldCenter.Y );
			using var pen = new Pen( Color.Lime, 1f / Math.Max( Zoom, 0.0001f ) );
			using var selPen = new Pen( Color.OrangeRed, 2f / Math.Max( Zoom, 0.0001f ) );
			using var nameBrush = new SolidBrush( Color.White );
			using var font = new Font( FontFamily.GenericSansSerif, 10f / Math.Max( Zoom, 0.0001f ) );

			foreach( var sp in _model.SpritesInOrder )
			{
				if( _filterId.HasValue && sp.TextureId != _filterId.Value )
					continue;
				string? texName = null;
				if( sp.TextureId >= 0 && sp.TextureId < _model.DenseToRaw.Count )
				{
					int rawId = _model.DenseToRaw[ sp.TextureId ];
					if( _model.ResourceById.TryGetValue( rawId, out var nm ) )
						texName = nm;
				}
				else
				{
					texName = _fallbackTex;
				}
				if( texName == null )
					continue;
				var bmp = _resolver.Resolve( texName );
				if( bmp == null )
					continue;
				var ib = new Rectangle( 0, 0, bmp.Width, bmp.Height );
				var isect = Rectangle.Intersect( sp.SrcRect, ib );
				if( isect.Width <= 0 || isect.Height <= 0 )
					continue;
				var dst = new RectangleF( isect.X, isect.Y, isect.Width, isect.Height );
				e.Graphics.DrawImage( bmp, dst, isect, System.Drawing.GraphicsUnit.Pixel );
				e.Graphics.DrawRectangle( ReferenceEquals( sp, SelectedSprite ) ? selPen : pen, sp.SrcRect.X, sp.SrcRect.Y, sp.SrcRect.Width, sp.SrcRect.Height );
				if( !string.IsNullOrEmpty( sp.Name ) )
					e.Graphics.DrawString( sp.Name, font, nameBrush, sp.SrcRect.X + 2, sp.SrcRect.Y + 2 );
			}
		}

		void HitTest( Point pt )
		{
			if( _model == null )
				return;
			var canvasCenter = new PointF( ClientSize.Width * 0.5f + Pan.X, ClientSize.Height * 0.5f + Pan.Y );
			var invX = ( pt.X - canvasCenter.X ) / Math.Max( Zoom, 0.0001f ) + _worldCenter.X;
			var invY = ( pt.Y - canvasCenter.Y ) / Math.Max( Zoom, 0.0001f ) + _worldCenter.Y;
			var inv = new PointF( invX, invY );
			for( int i = _model.SpritesInOrder.Count - 1; i >= 0; i-- )
			{
				var sp = _model.SpritesInOrder[ i ];
				if( _filterId.HasValue && sp.TextureId != _filterId.Value )
					continue;
				if( sp.SrcRect.Contains( Point.Round( inv ) ) )
				{
					SelectedSprite = sp;
					Invalidate();
					SelectionChanged?.Invoke( this, EventArgs.Empty );
					return;
				}
			}
		}
	}
}
