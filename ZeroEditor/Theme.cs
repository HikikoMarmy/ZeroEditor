using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class DarkPalette
{
	public static readonly Color Window = Color.FromArgb( 42, 42, 42 );
	public static readonly Color Surface = Color.FromArgb( 42, 42, 42 );
	public static readonly Color Sunken = Color.FromArgb( 50, 50, 50 );
	public static readonly Color Border = Color.FromArgb( 64, 64, 64 );
	public static readonly Color Fore = Color.FromArgb( 225, 225, 225 );
	public static readonly Color MutedFore = Color.FromArgb( 180, 180, 180 );
	public static readonly Color Accent = Color.FromArgb( 0, 120, 215 );
}

sealed class DarkColorTable : ProfessionalColorTable
{
	public DarkColorTable()
	{
		UseSystemColors = false;
	}
	public override Color ToolStripDropDownBackground => DarkPalette.Sunken;
	public override Color MenuBorder => DarkPalette.Border;
	public override Color MenuItemBorder => DarkPalette.Border;
	public override Color MenuItemSelected => Color.FromArgb( 70, DarkPalette.Accent );
	public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
	public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
	public override Color MenuItemPressedGradientBegin => DarkPalette.Surface;
	public override Color MenuItemPressedGradientEnd => DarkPalette.Surface;
	public override Color MenuStripGradientBegin => DarkPalette.Surface;
	public override Color MenuStripGradientEnd => DarkPalette.Surface;
	public override Color ToolStripGradientBegin => DarkPalette.Surface;
	public override Color ToolStripGradientMiddle => DarkPalette.Surface;
	public override Color ToolStripGradientEnd => DarkPalette.Surface;
	public override Color ImageMarginGradientBegin => DarkPalette.Sunken;
	public override Color ImageMarginGradientMiddle => DarkPalette.Sunken;
	public override Color ImageMarginGradientEnd => DarkPalette.Sunken;
	public override Color SeparatorDark => DarkPalette.Border;
	public override Color SeparatorLight => DarkPalette.Border;
	public override Color ToolStripBorder => DarkPalette.Border;
	public override Color ToolStripContentPanelGradientBegin => DarkPalette.Surface;
	public override Color ToolStripContentPanelGradientEnd => DarkPalette.Surface;
	public override Color OverflowButtonGradientBegin => DarkPalette.Surface;
	public override Color OverflowButtonGradientMiddle => DarkPalette.Surface;
	public override Color OverflowButtonGradientEnd => DarkPalette.Surface;
	public override Color GripDark => DarkPalette.Border;
	public override Color GripLight => DarkPalette.Border;
}

sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
	public DarkToolStripRenderer() : base( new DarkColorTable() ) { }
	protected override void OnRenderItemText( ToolStripItemTextRenderEventArgs e )
	{
		bool selected = e.Item.Selected || ( e.Item as ToolStripMenuItem )?.Pressed == true;
		e.TextColor = selected ? DarkPalette.Fore : DarkPalette.MutedFore;
		base.OnRenderItemText( e );
	}
	protected override void OnRenderMenuItemBackground( ToolStripItemRenderEventArgs e )
	{
		var rect = new Rectangle( Point.Empty, e.Item.Bounds.Size );
		bool selected = e.Item.Selected || ( e.Item as ToolStripMenuItem )?.Pressed == true;
		using var bg = new SolidBrush( selected ? Color.FromArgb( 70, DarkPalette.Accent ) : e.ToolStrip.BackColor );
		e.Graphics.FillRectangle( bg, rect );
		if( selected )
		{
			using var pen = new Pen( DarkPalette.Border );
			e.Graphics.DrawRectangle( pen, new Rectangle( 0, 0, rect.Width - 1, rect.Height - 1 ) );
		}
	}
}

static class NativeDark
{
	const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
	const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	[DllImport( "dwmapi.dll" )]
	static extern int DwmSetWindowAttribute( IntPtr hwnd, int attr, ref int attrValue, int attrSize );

	[DllImport( "uxtheme.dll", CharSet = CharSet.Unicode )]
	static extern int SetWindowTheme( IntPtr hWnd, string appName, string idList );

	public static void EnableImmersiveDarkMode( IntPtr hwnd )
	{
		if( hwnd == IntPtr.Zero )
			return;
		int use = 1;
		DwmSetWindowAttribute( hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref use, sizeof( int ) );
		DwmSetWindowAttribute( hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref use, sizeof( int ) );
	}

	public static void ApplyDarkExplorerTheme( IntPtr hwnd )
	{
		if( hwnd == IntPtr.Zero )
			return;
		SetWindowTheme( hwnd, "DarkMode_Explorer", null );
	}
}

public static class ThemeManager
{
	static bool _rendererApplied;

	public static void ApplyDark( Control root )
	{
		if( root == null )
			return;
		if( !_rendererApplied )
		{
			ApplyGlobalToolStripRenderer();
			_rendererApplied = true;
		}
		root.BackColor = DarkPalette.Window;
		root.ForeColor = DarkPalette.Fore;
		ApplyPerControlOverrides( root );
		foreach( Control child in root.Controls )
			ApplyDark( child );
	}

	static void ApplyGlobalToolStripRenderer()
	{
		ToolStripManager.VisualStylesEnabled = false;
		ToolStripManager.Renderer = new DarkToolStripRenderer();
	}

	static void StyleToolStripItems( ToolStripItemCollection items )
	{
		foreach( ToolStripItem it in items )
		{
			it.ForeColor = DarkPalette.Fore;
			if( it is ToolStripSeparator sep )
				sep.BackColor = DarkPalette.Sunken;
			if( it is ToolStripDropDownItem ddi )
			{
				ddi.DropDown.BackColor = DarkPalette.Sunken;
				ddi.DropDown.ForeColor = DarkPalette.Fore;
				ddi.DropDown.RenderMode = ToolStripRenderMode.ManagerRenderMode;
				StyleToolStripItems( ddi.DropDownItems );
			}
		}
	}

	static void StyleDataGridViewColumns( DataGridView dgv )
	{
		foreach( DataGridViewColumn col in dgv.Columns )
		{
			col.DefaultCellStyle ??= new DataGridViewCellStyle();
			col.DefaultCellStyle.BackColor = DarkPalette.Sunken;
			col.DefaultCellStyle.ForeColor = DarkPalette.Fore;
			col.DefaultCellStyle.SelectionBackColor = Color.FromArgb( 70, DarkPalette.Accent );
			col.DefaultCellStyle.SelectionForeColor = DarkPalette.Fore;
			switch( col )
			{
				case DataGridViewLinkColumn l:
				l.LinkColor = DarkPalette.Accent;
				l.ActiveLinkColor = DarkPalette.Accent;
				l.VisitedLinkColor = DarkPalette.Accent;
				l.TrackVisitedState = false;
				l.DefaultCellStyle.SelectionForeColor = DarkPalette.Fore;
				break;
				case DataGridViewCheckBoxColumn chk:
				chk.FlatStyle = FlatStyle.Flat;
				break;
				case DataGridViewButtonColumn btn:
				btn.FlatStyle = FlatStyle.Flat;
				btn.DefaultCellStyle.BackColor = DarkPalette.Surface;
				btn.DefaultCellStyle.ForeColor = DarkPalette.Fore;
				btn.DefaultCellStyle.SelectionBackColor = Color.FromArgb( 70, DarkPalette.Accent );
				btn.DefaultCellStyle.SelectionForeColor = DarkPalette.Fore;
				break;
				case DataGridViewComboBoxColumn cb:
				cb.FlatStyle = FlatStyle.Flat;
				break;
			}
		}
	}

	static void SetControlStyle( Control c, ControlStyles styles, bool value )
	{
		var mi = typeof( Control ).GetMethod( "SetStyle", BindingFlags.Instance | BindingFlags.NonPublic );
		mi?.Invoke( c, new object[] { styles, value } );
	}

	static void EnableDoubleBuffering( Control c )
	{
		if( SystemInformation.TerminalServerSession )
			return;
		var pi = typeof( Control ).GetProperty( "DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic );
		pi?.SetValue( c, true, null );
	}

	static void ApplyScrollbarThemeNowOrOnHandle( Control c )
	{
		void apply()
		{
			NativeDark.ApplyDarkExplorerTheme( c.Handle );
			var form = c.FindForm();
			if( form != null && form.IsHandleCreated )
				NativeDark.EnableImmersiveDarkMode( form.Handle );
		}
		if( c.IsHandleCreated )
			apply();
		else
			c.HandleCreated += ( _, __ ) => apply();
	}

	static void ApplyDarkScrollbars( Control c )
	{
		switch( c )
		{
			case CheckedListBox:
			case ListBox:
			case TableLayoutPanel:
			case Panel:
			case TextBox:
			case RichTextBox:
			case ListView:
			case TreeView:
			case DataGridView:
			case PropertyGrid:
			case SplitContainer:
			ApplyScrollbarThemeNowOrOnHandle( c );
			break;
		}

	}

	static void ApplyPerControlOverrides( Control c )
	{
		switch( c )
		{
			case Form f:
			f.BackColor = DarkPalette.Window;
			f.ForeColor = DarkPalette.Fore;
			if( f.IsHandleCreated )
				NativeDark.EnableImmersiveDarkMode( f.Handle );
			else
				f.HandleCreated += ( _, __ ) => NativeDark.EnableImmersiveDarkMode( f.Handle );
			break;

			case MenuStrip ms:
			ms.RenderMode = ToolStripRenderMode.ManagerRenderMode;
			ms.BackColor = DarkPalette.Surface;
			ms.ForeColor = DarkPalette.Fore;
			StyleToolStripItems( ms.Items );
			break;

			case StatusStrip ss:
			ss.RenderMode = ToolStripRenderMode.ManagerRenderMode;
			ss.BackColor = DarkPalette.Surface;
			ss.ForeColor = DarkPalette.MutedFore;
			StyleToolStripItems( ss.Items );
			break;

			case ContextMenuStrip cms:
			cms.RenderMode = ToolStripRenderMode.ManagerRenderMode;
			cms.BackColor = DarkPalette.Sunken;
			cms.ForeColor = DarkPalette.Fore;
			StyleToolStripItems( cms.Items );
			break;

			case ToolStrip ts:
			ts.RenderMode = ToolStripRenderMode.ManagerRenderMode;
			ts.BackColor = DarkPalette.Surface;
			ts.ForeColor = DarkPalette.Fore;
			ts.GripMargin = new Padding( 2 );
			ts.Padding = new Padding( 4, 2, 4, 2 );
			StyleToolStripItems( ts.Items );
			break;

			case SplitContainer sc:
			sc.BackColor = DarkPalette.Surface;
			sc.Panel1.BackColor = DarkPalette.Surface;
			sc.Panel2.BackColor = DarkPalette.Surface;
			sc.ForeColor = DarkPalette.Fore;
			sc.BorderStyle = BorderStyle.None;
			sc.Paint += ( s, e ) => e.Graphics.FillRectangle( new SolidBrush( DarkPalette.Border ), sc.SplitterRectangle );
			ApplyDarkScrollbars( sc );
			break;

			case GroupBox gb:
			gb.BackColor = DarkPalette.Surface;
			gb.ForeColor = DarkPalette.Fore;
			gb.Paint += ( s, e ) =>
			{
				e.Graphics.Clear( gb.BackColor );
				using var textBrush = new SolidBrush( gb.ForeColor );
				using var borderPen = new Pen( DarkPalette.Border );
				var textSize = e.Graphics.MeasureString( gb.Text, gb.Font );
				e.Graphics.DrawString( gb.Text, gb.Font, textBrush, 8, -1 );
				int y = (int)( textSize.Height / 2 );
				e.Graphics.DrawLine( borderPen, 1, y, 6, y );
				e.Graphics.DrawLine( borderPen, (int)textSize.Width + 10, y, gb.Width - 2, y );
				e.Graphics.DrawLine( borderPen, 1, y, 1, gb.Height - 2 );
				e.Graphics.DrawLine( borderPen, gb.Width - 2, y, gb.Width - 2, gb.Height - 2 );
				e.Graphics.DrawLine( borderPen, 1, gb.Height - 2, gb.Width - 2, gb.Height - 2 );
			};
			break;

			case TableLayoutPanel:
			case Panel:
			c.BackColor = DarkPalette.Surface;
			ApplyDarkScrollbars( c );
			break;

			case Label lbl:
			lbl.BackColor = Color.Transparent;
			lbl.ForeColor = DarkPalette.MutedFore;
			break;

			case Button btn:
			btn.FlatStyle = FlatStyle.Flat;
			btn.FlatAppearance.BorderColor = DarkPalette.Border;
			btn.FlatAppearance.BorderSize = 1;
			btn.BackColor = DarkPalette.Sunken;
			btn.ForeColor = DarkPalette.Fore;
			btn.MouseEnter += ( _, __ ) => btn.BackColor = DarkPalette.Surface;
			btn.MouseLeave += ( _, __ ) => btn.BackColor = DarkPalette.Sunken;
			break;

			case TextBox tb:
			tb.BackColor = DarkPalette.Sunken;
			tb.ForeColor = DarkPalette.Fore;
			tb.BorderStyle = BorderStyle.FixedSingle;
			ApplyDarkScrollbars( tb );
			break;

			case NumericUpDown nud:
			nud.BackColor = DarkPalette.Sunken;
			nud.ForeColor = DarkPalette.Fore;
			nud.BorderStyle = BorderStyle.FixedSingle;
			if( nud.Controls.Count > 0 )
				nud.Controls[ 0 ].BackColor = DarkPalette.Sunken;
			break;

			case ComboBox cb:
			cb.FlatStyle = FlatStyle.Flat;
			cb.BackColor = DarkPalette.Sunken;
			cb.ForeColor = DarkPalette.Fore;
			break;

			case TreeView tv:
			tv.BackColor = DarkPalette.Sunken;
			tv.ForeColor = DarkPalette.Fore;
			tv.BorderStyle = BorderStyle.None;
			tv.ShowLines = false;
			tv.HideSelection = false;
			tv.LineColor = DarkPalette.Border;
			tv.DrawMode = TreeViewDrawMode.OwnerDrawText;
			tv.DrawNode += ( s, e ) =>
			{
				var isSelected = ( e.State & TreeNodeStates.Selected ) != 0;
				e.Graphics.FillRectangle( new SolidBrush( isSelected ? Color.FromArgb( 70, DarkPalette.Accent ) : tv.BackColor ), e.Bounds );
				TextRenderer.DrawText( e.Graphics, e.Node.Text, tv.Font, e.Bounds.Location, isSelected ? DarkPalette.Fore : DarkPalette.MutedFore );
			};
			ApplyDarkScrollbars( tv );
			break;

			case ListView lv:
			lv.BackColor = DarkPalette.Sunken;
			lv.ForeColor = DarkPalette.Fore;
			lv.BorderStyle = BorderStyle.None;
			lv.OwnerDraw = true;
			lv.DrawColumnHeader += ( s, e ) =>
			{
				e.Graphics.FillRectangle( new SolidBrush( DarkPalette.Surface ), e.Bounds );
				TextRenderer.DrawText( e.Graphics, e.Header.Text, lv.Font, e.Bounds, DarkPalette.MutedFore );
				e.Graphics.DrawLine( new Pen( DarkPalette.Border ), e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1 );
			};
			lv.DrawItem += ( s, e ) =>
			{
				e.DrawBackground();
				bool sel = ( e.State & ListViewItemStates.Selected ) != 0;
				var bg = sel ? Color.FromArgb( 70, DarkPalette.Accent ) : DarkPalette.Sunken;
				e.Graphics.FillRectangle( new SolidBrush( bg ), e.Bounds );
				TextRenderer.DrawText( e.Graphics, e.Item.Text, lv.Font, e.Bounds, DarkPalette.Fore );
			};
			lv.DrawSubItem += ( s, e ) =>
			{
				TextRenderer.DrawText( e.Graphics, e.SubItem.Text, lv.Font, e.Bounds, DarkPalette.Fore );
			};
			ApplyDarkScrollbars( lv );
			break;

			case TabControl tc:
			tc.DrawMode = TabDrawMode.OwnerDrawFixed;
			SetControlStyle( tc, ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true );
			EnableDoubleBuffering( tc );
			tc.Padding = new Point( 16, 6 );
			tc.DrawItem += ( s, e ) =>
			{
				var sel = ( e.State & DrawItemState.Selected ) != 0;
				var page = tc.TabPages[ e.Index ];
				var bounds = e.Bounds;
				using var bg = new SolidBrush( sel ? DarkPalette.Surface : DarkPalette.Window );
				e.Graphics.FillRectangle( bg, bounds );
				TextRenderer.DrawText( e.Graphics, page.Text, tc.Font, bounds, sel ? DarkPalette.Fore : DarkPalette.MutedFore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter );
				e.Graphics.DrawRectangle( new Pen( DarkPalette.Border ), bounds );
			};
			tc.BackColor = DarkPalette.Window;
			tc.ForeColor = DarkPalette.Fore;
			break;

			case DataGridView dgv:
			EnableDoubleBuffering( dgv );
			dgv.BackgroundColor = DarkPalette.Sunken;
			dgv.BorderStyle = BorderStyle.None;
			dgv.GridColor = DarkPalette.Border;
			dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
			dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
			dgv.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
			dgv.EnableHeadersVisualStyles = false;
			dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = DarkPalette.Surface,
				ForeColor = DarkPalette.MutedFore,
				SelectionBackColor = Color.FromArgb( 70, DarkPalette.Accent ),
				SelectionForeColor = DarkPalette.Fore,
				Alignment = DataGridViewContentAlignment.MiddleLeft,
				WrapMode = DataGridViewTriState.True
			};
			dgv.RowHeadersDefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = DarkPalette.Surface,
				ForeColor = DarkPalette.MutedFore,
				SelectionBackColor = Color.FromArgb( 70, DarkPalette.Accent ),
				SelectionForeColor = DarkPalette.Fore,
				WrapMode = DataGridViewTriState.True
			};
			dgv.DefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = DarkPalette.Sunken,
				ForeColor = DarkPalette.Fore,
				SelectionBackColor = Color.FromArgb( 70, DarkPalette.Accent ),
				SelectionForeColor = DarkPalette.Fore,
				WrapMode = DataGridViewTriState.False
			};
			dgv.RowsDefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = DarkPalette.Sunken,
				ForeColor = DarkPalette.Fore
			};
			dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = Color.FromArgb( 46, 46, 46 ),
				ForeColor = DarkPalette.Fore
			};
			dgv.RowTemplate.Height = Math.Max( dgv.RowTemplate.Height, 22 );
			dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			dgv.ColumnAdded += ( _, __ ) => StyleDataGridViewColumns( dgv );
			dgv.DataBindingComplete += ( _, __ ) => StyleDataGridViewColumns( dgv );
			StyleDataGridViewColumns( dgv );
			dgv.EditingControlShowing += ( _, e ) =>
			{
				if( e.Control is TextBox tb )
				{
					tb.BackColor = DarkPalette.Sunken;
					tb.ForeColor = DarkPalette.Fore;
					tb.BorderStyle = BorderStyle.FixedSingle;
				}
				else if( e.Control is ComboBox cb )
				{
					cb.FlatStyle = FlatStyle.Flat;
					cb.BackColor = DarkPalette.Sunken;
					cb.ForeColor = DarkPalette.Fore;
				}
			};
			ApplyDarkScrollbars( dgv );
			break;
		}
	}
}
