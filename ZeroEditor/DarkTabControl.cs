public sealed class DarkTabControl : TabControl
{
    public DarkTabControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

        BackColor = DarkPalette.Window;
        ForeColor = DarkPalette.Fore;

        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(140, 28);
        Padding = new Point(12, 6);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        BackColor = DarkPalette.Window;
        ForeColor = DarkPalette.Fore;

        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(140, 28);
        Padding = new Point(12, 6);

        for (int i = 0; i < TabPages.Count; i++)
        {
            var p = TabPages[i];
            p.UseVisualStyleBackColor = false;
            p.BackColor = DarkPalette.Window;
            p.ForeColor = DarkPalette.Fore;
        }

        Invalidate();
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);

        if (e.Control is TabPage p)
        {
            p.UseVisualStyleBackColor = false;
            p.BackColor = DarkPalette.Window;
            p.ForeColor = DarkPalette.Fore;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var bg = new SolidBrush(DarkPalette.Window);
        pevent.Graphics.FillRectangle(bg, ClientRectangle);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics;
        var rect = GetTabRect(e.Index);
        bool sel = (e.State & DrawItemState.Selected) != 0;

        using var bg = new SolidBrush(sel ? DarkPalette.Surface : DarkPalette.Window);
        using var pen = new Pen(DarkPalette.Border);

        g.FillRectangle(bg, rect);
        g.DrawRectangle(pen, rect);

        var text = TabPages[e.Index].Text ?? "";
        var color = sel ? DarkPalette.Fore : DarkPalette.MutedFore;

        TextRenderer.DrawText(
            g,
            text,
            Font,
            rect,
            color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        );
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_ERASEBKGND = 0x0014;

        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }
}
