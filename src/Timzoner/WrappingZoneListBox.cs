namespace Timzoner;

internal sealed class WrappingZoneListBox : ListBox
{
    private bool refreshing;
    private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

    public WrappingZoneListBox()
    {
        IntegralHeight = false;
        ScrollAlwaysVisible = true;
        DrawMode = DrawMode.OwnerDrawVariable;
    }

    private int TextWidth => Math.Max(1, ClientSize.Width - LogicalToDeviceUnits(12));

    protected override void OnMeasureItem(MeasureItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var size = TextRenderer.MeasureText(e.Graphics, GetItemText(Items[e.Index]), Font,
            new Size(TextWidth, int.MaxValue), TextFlags);
        e.ItemHeight = Math.Max(Font.Height, size.Height) + LogicalToDeviceUnits(8);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        e.DrawBackground();
        var bounds = e.Bounds;
        bounds.X += LogicalToDeviceUnits(6);
        bounds.Y += LogicalToDeviceUnits(4);
        bounds.Width = TextWidth;
        bounds.Height -= LogicalToDeviceUnits(8);
        var color = (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : ForeColor;
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, bounds, color, TextFlags);
        e.DrawFocusRectangle();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (refreshing || Items.Count == 0) return;
        refreshing = true;
        try { RefreshItems(); }
        finally { refreshing = false; }
    }
}
