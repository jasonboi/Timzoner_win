namespace Timzoner;

internal sealed class WrappingZoneListBox : ListBox
{
    private bool refreshing;
    private int hotIndex = -1;
    private int measuredWidth = -1;
    private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

    public WrappingZoneListBox()
    {
        IntegralHeight = false;
        ScrollAlwaysVisible = true;
        BorderStyle = BorderStyle.None;
        DrawMode = DrawMode.OwnerDrawVariable;
    }

    private int TextInset => LogicalToDeviceUnits(16);
    private int TextWidth => Math.Max(1, ClientSize.Width - TextInset - LogicalToDeviceUnits(12));

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyNative(this);
    }

    protected override void OnMeasureItem(MeasureItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var size = TextRenderer.MeasureText(e.Graphics, GetItemText(Items[e.Index]), Font,
            new Size(TextWidth, int.MaxValue), TextFlags);
        e.ItemHeight = Math.Max(Font.Height, size.Height) + LogicalToDeviceUnits(14);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        bool isSelected = (e.State & DrawItemState.Selected) != 0;
        var text = new Rectangle(e.Bounds.X + TextInset, e.Bounds.Y + LogicalToDeviceUnits(7), TextWidth, e.Bounds.Height - LogicalToDeviceUnits(14));
        if (Theme.HighContrast)
        {
            e.DrawBackground();
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text, isSelected ? SystemColors.HighlightText : ForeColor, TextFlags);
            e.DrawFocusRectangle();
            return;
        }
        var p = Theme.Colors;
        using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, e.Bounds);
        int inset = LogicalToDeviceUnits(4);
        var item = new RectangleF(e.Bounds.X + inset, e.Bounds.Y + LogicalToDeviceUnits(2), e.Bounds.Width - inset * 2, e.Bounds.Height - LogicalToDeviceUnits(4));
        if (isSelected || e.Index == hotIndex)
            Theme.FillRounded(e.Graphics, item, inset, isSelected ? p.ListSelected : p.ListHover);
        if (isSelected)
        {
            float pill = Math.Min(LogicalToDeviceUnits(16), item.Height - LogicalToDeviceUnits(8));
            Theme.FillRounded(e.Graphics, new RectangleF(item.X, item.Y + (item.Height - pill) / 2, LogicalToDeviceUnits(3), pill), LogicalToDeviceUnits(2), p.Accent);
        }
        if ((e.State & DrawItemState.Focus) != 0 && (e.State & DrawItemState.NoFocusRect) == 0)
        {
            using var pen = new Pen(p.Text);
            var mode = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Theme.RoundedRectangle(new RectangleF(item.X + 0.5f, item.Y + 0.5f, item.Width - 1, item.Height - 1), inset);
            e.Graphics.DrawPath(pen, path);
            e.Graphics.SmoothingMode = mode;
        }
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, text, ForeColor, TextFlags);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        // IndexFromPoint can return Items.Count below the last item, so validate the range before asking for bounds.
        int index = IndexFromPoint(e.Location);
        if (index < 0 || index >= Items.Count || !GetItemRectangle(index).Contains(e.Location)) index = NoMatches;
        SetHot(index);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHot(NoMatches);
    }

    private void SetHot(int index)
    {
        if (index == hotIndex) return;
        int previous = hotIndex;
        hotIndex = index;
        if (previous >= 0 && previous < Items.Count) Invalidate(GetItemRectangle(previous));
        if (index >= 0 && index < Items.Count) Invalidate(GetItemRectangle(index));
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        // Wrapped item heights depend only on width, so height-only resizes from window managers skip the full re-measure.
        if (refreshing || Items.Count == 0 || ClientSize.Width == measuredWidth) return;
        measuredWidth = ClientSize.Width;
        refreshing = true;
        try { RefreshItems(); }
        finally { refreshing = false; }
    }
}
