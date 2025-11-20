using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZeroEditor
{
	public partial class WorkIndicator : UserControl
	{
		private readonly System.Windows.Forms.Timer _timer;
		private float _angle;
		private readonly Label _label;

		public WorkIndicator()
		{
			DoubleBuffered = true;
			Dock = DockStyle.Fill;
			BackColor = Color.FromArgb( 32, 0, 0, 0 );

			_label = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 24,
				TextAlign = ContentAlignment.MiddleCenter,
				ForeColor = Color.White,
				Text = "Working..."
			};

			Controls.Add( _label );

			_timer = new System.Windows.Forms.Timer { Interval = 10 };
			_timer.Tick += ( s, e ) =>
			{
				_angle += 10f;
				if( _angle >= 360f )
					_angle -= 360f;
				Invalidate();
			};

			Visible = false;
		}

		public string Message
		{
			get => _label.Text;
			set => _label.Text = value;
		}

		public void StartAnimating( string? message = null )
		{
			if( message != null )
				Message = message;

			_angle = 0f;
			Visible = true;
			_timer.Start();
		}

		public void StopAnimating()
		{
			_timer.Stop();
			Visible = false;
		}

		protected override void OnPaint( PaintEventArgs e )
		{
			base.OnPaint( e );

			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			int availableHeight = ClientSize.Height - _label.Height - 10;

			const int maxSpinnerSize = 200;
			int size = Math.Min( Math.Min( ClientSize.Width, availableHeight ), maxSpinnerSize );
			if( size <= 0 )
				return;

			int cx = ClientSize.Width / 2;
			int cy = ( ClientSize.Height - _label.Height ) / 2;

			size -= 10;
			if( size <= 0 )
				return;

			var rect = new Rectangle(
				cx - size / 2,
				cy - size / 2,
				size,
				size );

			using var penBg = new Pen( Color.FromArgb( 80, Color.DeepSkyBlue ), 4 );
			using var penFg = new Pen( Color.DeepSkyBlue, 2 );

			g.DrawArc( penBg, rect, 0, 360 );
			g.DrawArc( penFg, rect, _angle, 90 );
		}
	}
}
