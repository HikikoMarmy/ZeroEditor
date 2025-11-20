using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroEditor.Zero1.DataType;

namespace ZeroEditor.Editors.Elf.Zero1
{
	public partial class FogEditor : UserControl
	{
		private enum SetKind { World, FirstPerson }

		private readonly IList<FogParams> _baseParams;
		private readonly IList<FogRgb> _baseRgb;
		private readonly IList<FogParams> _finderParams;
		private readonly IList<FogRgb> _finderRgb;

		private IList<FogParams> _activeParams;
		private IList<FogRgb> _activeRgb;

		private int _index = -1;
		private bool _updating;

		public FogEditor(
			IList<FogParams> fogParams,
			IList<FogRgb> fogRgb,
			IList<FogParams> fogParamFinder,
			IList<FogRgb> fogRgbFinder )
		{
			_baseParams = fogParams ?? Array.Empty<FogParams>();
			_baseRgb = fogRgb ?? Array.Empty<FogRgb>();
			_finderParams = fogParamFinder ?? Array.Empty<FogParams>();
			_finderRgb = fogRgbFinder ?? Array.Empty<FogRgb>();
			_activeParams = _baseParams;
			_activeRgb = _baseRgb;

			InitializeComponent();
			ThemeManager.ApplyDark( this );

			cmbSet.SelectedIndex = 0;
			cmbSet.SelectedIndexChanged += ( _, __ ) => { SwitchSet(); pnlRange.Invalidate(); };
			lst.SelectedIndexChanged += ( _, __ ) => SelectIndex( lst.SelectedIndex );

			numFar.ValueChanged += ( _, __ ) => { ApplyParams(); pnlRange.Invalidate(); };
			numNear.ValueChanged += ( _, __ ) => { ApplyParams(); pnlRange.Invalidate(); };
			numMax.ValueChanged += ( _, __ ) => { ApplyParams(); pnlRange.Invalidate(); };
			numMin.ValueChanged += ( _, __ ) => { ApplyParams(); pnlRange.Invalidate(); };

			numR.ValueChanged += ( _, __ ) => { ApplyRgb(); pnlRange.Invalidate(); };
			numG.ValueChanged += ( _, __ ) => { ApplyRgb(); pnlRange.Invalidate(); };
			numB.ValueChanged += ( _, __ ) => { ApplyRgb(); pnlRange.Invalidate(); };
			numA.ValueChanged += ( _, __ ) => { ApplyRgb(); pnlRange.Invalidate(); };

			pnlRange.GetActive = () =>
			{
				if( _index < 0 || _index >= ( _activeParams?.Count ?? 0 ) || _index >= ( _activeRgb?.Count ?? 0 ) )
					return null;
				return (_activeParams[ _index ], _activeRgb[ _index ]);
			};

			Bind();
		}

		private void Bind()
		{
			lst.BeginUpdate();
			lst.Items.Clear();
			int count = Math.Max( _activeParams?.Count ?? 0, _activeRgb?.Count ?? 0 );
			for( int i = 0; i < count; i++ )
				lst.Items.Add( $"Fog {i}" );
			lst.EndUpdate();
			if( count > 0 )
				lst.SelectedIndex = 0;
		}

		private void SwitchSet()
		{
			switch( (SetKind)cmbSet.SelectedIndex )
			{
				case SetKind.World:
				_activeParams = _baseParams;
				_activeRgb = _baseRgb;
				break;
				case SetKind.FirstPerson:
				_activeParams = _finderParams;
				_activeRgb = _finderRgb;
				break;
			}
			Bind();
		}

		private void SelectIndex( int idx )
		{
			_index = idx;
			LoadEntry();
		}

		private void LoadEntry()
		{
			if( _index < 0 || _index >= ( _activeParams?.Count ?? 0 ) || _index >= ( _activeRgb?.Count ?? 0 ) )
				return;

			_updating = true;

			var p = _activeParams[ _index ];
			var c = _activeRgb[ _index ];

			numFar.Value = Clamp( numFar, (decimal)p.fog_far );
			numNear.Value = Clamp( numNear, (decimal)p.fog_near );
			numMax.Value = Clamp( numMax, (decimal)p.fog_max );
			numMin.Value = Clamp( numMin, (decimal)p.fog_min );

			numR.Value = Clamp( numR, c.r );
			numG.Value = Clamp( numG, c.g );
			numB.Value = Clamp( numB, c.b );
			numA.Value = Clamp( numA, c.a );

			pnlColor.BackColor = ToColor( c );
			pnlRange.Invalidate();

			_updating = false;
		}

		private static decimal Clamp( NumericUpDown n, decimal v )
		{
			if( v < n.Minimum )
				return n.Minimum;
			if( v > n.Maximum )
				return n.Maximum;
			return v;
		}

		private static decimal Clamp( NumericUpDown n, int v )
		{
			if( v < (int)n.Minimum )
				return n.Minimum;
			if( v > (int)n.Maximum )
				return n.Maximum;
			return v;
		}

		private void ApplyParams()
		{
			if( _updating || _index < 0 )
				return;
			if( _index >= _activeParams.Count )
				return;

			var p = _activeParams[ _index ];
			p.fog_far = (float)numFar.Value;
			p.fog_near = (float)numNear.Value;
			p.fog_max = (float)numMax.Value;
			p.fog_min = (float)numMin.Value;
			_activeParams[ _index ] = p;
		}

		private void ApplyRgb()
		{
			if( _updating || _index < 0 )
				return;
			if( _index >= _activeRgb.Count )
				return;

			var c = _activeRgb[ _index ];
			c.r = (int)numR.Value;
			c.g = (int)numG.Value;
			c.b = (int)numB.Value;
			c.a = (int)numA.Value;
			_activeRgb[ _index ] = c;

			pnlColor.BackColor = ToColor( c );
		}

		private static Color ToColor( FogRgb c )
		{
			int r = Math.Max( 0, Math.Min( 255, c.r ) );
			int g = Math.Max( 0, Math.Min( 255, c.g ) );
			int b = Math.Max( 0, Math.Min( 255, c.b ) );
			int a = Math.Max( 0, Math.Min( 255, c.a <= 0 ? 255 : c.a ) );
			return Color.FromArgb( a, r, g, b );
		}
	}

	internal sealed class FogRangePanel : Panel
	{
		public Func<(FogParams p, FogRgb c)?>? GetActive { get; set; }

		public FogRangePanel()
		{
			DoubleBuffered = true;
			Resize += ( _, __ ) => Invalidate();
			Paint += OnPaintFog;
		}

		private void OnPaintFog( object? sender, PaintEventArgs e )
		{
			var data = GetActive?.Invoke();
			var g = e.Graphics;
			var rect = ClientRectangle;

			using( var bg = new SolidBrush( Color.Black ) )
				g.FillRectangle( bg, rect );

			if( data == null )
				return;

			var (p, c) = data.Value;

			float min = p.fog_min;
			float max = p.fog_max;
			float near = p.fog_near;
			float far = p.fog_far;

			if( !( max > min ) )
			{
				float a = Math.Min( near, far ) - 1f;
				float b = Math.Max( near, far ) + 1f;
				min = a;
				max = b;
			}
			if( Math.Abs( max - min ) < 1e-6f )
				return;

			float nx = rect.Left + ( near - min ) / ( max - min ) * rect.Width;
			float fx = rect.Left + ( far - min ) / ( max - min ) * rect.Width;
			nx = Math.Max( rect.Left, Math.Min( rect.Right, nx ) );
			fx = Math.Max( rect.Left, Math.Min( rect.Right, fx ) );

			int x1 = (int)Math.Floor( Math.Min( nx, fx ) );
			int x2 = (int)Math.Ceiling( Math.Max( nx, fx ) );
			if( x2 <= x1 )
				x2 = x1 + 1;

			var fog = Color.FromArgb( Math.Max( 0, Math.Min( 255, c.a <= 0 ? 255 : c.a ) ),
									 Math.Max( 0, Math.Min( 255, c.r ) ),
									 Math.Max( 0, Math.Min( 255, c.g ) ),
									 Math.Max( 0, Math.Min( 255, c.b ) ) );

			using( var lgb = new LinearGradientBrush( new Rectangle( x1, rect.Top, Math.Max( 1, x2 - x1 ), rect.Height ),
													 Color.FromArgb( 0, fog ),
													 fog,
													 LinearGradientMode.Horizontal ) )
			{
				g.FillRectangle( lgb, new Rectangle( x1, rect.Top, Math.Max( 1, x2 - x1 ), rect.Height ) );
			}

			using var penNear = new Pen( Color.White, 2 );
			using var penFar = new Pen( Color.Gray, 2 );
			g.DrawLine( penNear, nx, rect.Top, nx, rect.Bottom );
			g.DrawLine( penFar, fx, rect.Top, fx, rect.Bottom );
		}
	}
}
