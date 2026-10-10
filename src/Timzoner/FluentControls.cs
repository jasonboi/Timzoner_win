namespace Timzoner;

internal sealed class FluentButton : Button
{
    private bool hover;
    private bool pressed;

    public FluentButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    [System.ComponentModel.DefaultValue(false)]
    public bool Accent { get; init; }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine);
        return new Size(Math.Max(LogicalToDeviceUnits(Accent ? 96 : 80), text.Width + LogicalToDeviceUnits(24)),
            Math.Max(LogicalToDeviceUnits(32), text.Height + LogicalToDeviceUnits(10)));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Theme.HighContrast) { base.OnPaint(e); return; }
        var p = Theme.Colors;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Surface);
        bool accent = Accent && Enabled;
        var fill = accent ? pressed ? p.AccentPressed : hover ? p.AccentHover : p.Accent
            : !Enabled ? p.Surface : pressed ? p.ControlPressed : hover ? p.ControlHover : p.Control;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = LogicalToDeviceUnits(4);
        Theme.FillRounded(g, bounds, radius, fill, accent ? null : p.Stroke);
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(p.Text, LogicalToDeviceUnits(2)) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset };
            var mode = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Theme.RoundedRectangle(bounds, radius);
            g.DrawPath(pen, path);
            g.SmoothingMode = mode;
        }
        var color = accent ? p.TextOnAccent : !Enabled ? p.TextDisabled : pressed ? p.TextSecondary : p.Text;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, color, fill,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
    }
}

internal sealed class FluentCheckBox : CheckBox
{
    private bool hover;

    public FluentCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
    }

    private int BoxSize => LogicalToDeviceUnits(20);
    private int Gap => LogicalToDeviceUnits(8);

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Theme.HighContrast) return base.GetPreferredSize(proposedSize);
        var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine);
        return new Size(BoxSize + Gap + text.Width + LogicalToDeviceUnits(2), Math.Max(LogicalToDeviceUnits(32), text.Height));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Theme.HighContrast) { base.OnPaint(e); return; }
        var p = Theme.Colors;
        var g = e.Graphics;
        var background = Parent?.BackColor ?? p.Surface;
        g.Clear(background);
        int size = BoxSize;
        var box = new RectangleF(0.5f, (Height - size) / 2 + 0.5f, size - 1, size - 1);
        float radius = LogicalToDeviceUnits(4);
        if (Checked)
        {
            Theme.FillRounded(g, box, radius, Enabled ? hover ? p.AccentHover : p.Accent : p.TextDisabled);
            Theme.DrawCheck(g, RectangleF.Inflate(box, -size * 0.12f, -size * 0.12f), p.TextOnAccent, Math.Max(1.5f, size / 12f));
        }
        else
        {
            var fill = hover && Enabled ? Theme.Blend(background, p.Text, 0.06) : Theme.Blend(background, p.Text, 0.02);
            Theme.FillRounded(g, box, radius, fill, Enabled ? p.StrongStroke : p.TextDisabled);
        }
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(p.Text, LogicalToDeviceUnits(1));
            var mode = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Theme.RoundedRectangle(RectangleF.Inflate(box, 2, 2), radius + 2);
            g.DrawPath(pen, path);
            g.SmoothingMode = mode;
        }
        var text = new Rectangle(size + Gap, 0, Width - size - Gap, Height);
        TextRenderer.DrawText(g, Text, Font, text, Enabled ? p.Text : p.TextDisabled, background,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

// Hosts a borderless text box or list inside a rounded Fluent input surface.
internal sealed class FluentFrame : Panel
{
    private readonly Control content;

    public FluentFrame(Control content)
    {
        this.content = content;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Dock = content.Dock;
        Margin = content.Margin;
        content.Dock = DockStyle.None;
        content.Margin = Padding.Empty;
        content.Enter += (_, _) => Invalidate();
        content.Leave += (_, _) => Invalidate();
        Controls.Add(content);
    }

    protected override void OnMouseDown(MouseEventArgs e) { content.Focus(); base.OnMouseDown(e); }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int border = LogicalToDeviceUnits(1);
        if (content is TextBox)
        {
            int inset = LogicalToDeviceUnits(10);
            content.Bounds = new Rectangle(inset, (Height - content.Height) / 2, Math.Max(1, Width - inset * 2), content.Height);
        }
        else
        {
            int inset = LogicalToDeviceUnits(4);
            content.Bounds = new Rectangle(border, inset, Math.Max(1, Width - border * 2), Math.Max(1, Height - inset * 2));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        if (Theme.HighContrast)
        {
            g.Clear(content.BackColor);
            ControlPaint.DrawBorder(g, ClientRectangle, SystemColors.WindowFrame, ButtonBorderStyle.Solid);
            return;
        }
        var p = Theme.Colors;
        g.Clear(Parent?.BackColor ?? p.Surface);
        float radius = LogicalToDeviceUnits(4);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRounded(g, bounds, radius, content.BackColor, p.Stroke);
        if (content is not TextBox) return;
        bool focused = content.ContainsFocus;
        int thickness = LogicalToDeviceUnits(focused ? 2 : 1);
        using var brush = new SolidBrush(focused ? p.Accent : p.StrongStroke);
        using var clip = g.Clip;
        using (var path = Theme.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius)) g.SetClip(path);
        g.FillRectangle(brush, 0, Height - thickness, Width, thickness);
        g.Clip = clip;
    }
}

// Paints the closed drop-down list itself so it matches Fluent buttons, keeping native selection and accessibility.
internal sealed class FluentComboBox : ComboBox
{
    private bool hover;

    public FluentComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ItemHeight = LogicalToDeviceUnits(26);
        Theme.ApplyNative(this);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ItemHeight = LogicalToDeviceUnits(26);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnDropDown(EventArgs e) { Invalidate(); base.OnDropDown(e); }
    protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        string text = GetItemText(Items[e.Index]) ?? "";
        if (Theme.HighContrast)
        {
            e.DrawBackground();
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
            return;
        }
        var p = Theme.Colors;
        using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, e.Bounds);
        int inset = LogicalToDeviceUnits(4);
        var item = new RectangleF(e.Bounds.X + inset, e.Bounds.Y + 1, e.Bounds.Width - inset * 2, e.Bounds.Height - 2);
        if ((e.State & DrawItemState.Selected) != 0) Theme.FillRounded(e.Graphics, item, inset, p.ListSelected);
        if (e.Index == SelectedIndex)
        {
            float pill = Math.Min(LogicalToDeviceUnits(16), item.Height - LogicalToDeviceUnits(8));
            Theme.FillRounded(e.Graphics, new RectangleF(item.X, item.Y + (item.Height - pill) / 2, LogicalToDeviceUnits(3), pill), LogicalToDeviceUnits(2), p.Accent);
        }
        var bounds = new Rectangle(e.Bounds.X + LogicalToDeviceUnits(14), e.Bounds.Y, e.Bounds.Width - LogicalToDeviceUnits(18), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, Font, bounds, p.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_PAINT = 0x000F;
        if (m.Msg != WM_PAINT || Theme.HighContrast) { base.WndProc(ref m); return; }
        var paint = default(PaintStruct);
        var hdc = BeginPaint(Handle, ref paint);
        try
        {
            using var buffer = BufferedGraphicsManager.Current.Allocate(hdc, ClientRectangle);
            PaintClosed(buffer.Graphics);
            buffer.Render();
        }
        finally { EndPaint(Handle, ref paint); }
    }

    private void PaintClosed(Graphics g)
    {
        var p = Theme.Colors;
        g.Clear(Parent?.BackColor ?? p.Surface);
        var fill = !Enabled ? p.Surface : DroppedDown ? p.ControlPressed : hover ? p.ControlHover : p.Control;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = LogicalToDeviceUnits(4);
        Theme.FillRounded(g, bounds, radius, fill, p.Stroke);
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(p.Text, LogicalToDeviceUnits(2)) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset };
            var mode = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Theme.RoundedRectangle(bounds, radius);
            g.DrawPath(pen, path);
            g.SmoothingMode = mode;
        }
        int arrow = LogicalToDeviceUnits(32);
        var text = new Rectangle(LogicalToDeviceUnits(11), 0, Math.Max(1, Width - arrow - LogicalToDeviceUnits(11)), Height);
        TextRenderer.DrawText(g, Text, Font, text, Enabled ? p.Text : p.TextDisabled, fill, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        float cx = Width - arrow / 2f, cy = Height / 2f, half = LogicalToDeviceUnits(4);
        var mode2 = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var pen = new Pen(Enabled ? p.TextSecondary : p.TextDisabled, Math.Max(1f, LogicalToDeviceUnits(1)))) g.DrawLines(pen, new[] { new PointF(cx - half, cy - half / 2), new PointF(cx, cy + half / 2), new PointF(cx + half, cy - half / 2) });
        g.SmoothingMode = mode2;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr Hdc; public int Erase; public int Left, Top, Right, Bottom; public int Restore; public int IncUpdate;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr window, ref PaintStruct paint);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EndPaint(IntPtr window, ref PaintStruct paint);
}
