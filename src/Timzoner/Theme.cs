using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Timzoner;

internal sealed record Palette(
    Color Surface, Color Control, Color ControlHover, Color ControlPressed, Color Input,
    Color Stroke, Color StrongStroke, Color Text, Color TextSecondary, Color TextDisabled,
    Color Accent, Color AccentHover, Color AccentPressed, Color TextOnAccent,
    Color Menu, Color MenuHover, Color ListHover, Color ListSelected, Color Critical);

internal static class Theme
{
    private static readonly bool RoundedCorners = Environment.OSVersion.Version.Build >= 22000;
    private static readonly ToolStripRenderer FluentRenderer = new FluentMenuRenderer();
    private static readonly ToolStripRenderer ContrastRenderer = new ToolStripProfessionalRenderer();

    public static bool IsDark { get; private set; }
    public static bool HighContrast => SystemInformation.HighContrast;
    public static Palette Colors { get; private set; } = Build(false, null);

    public static void Apply(string preference)
    {
        bool systemDark;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            systemDark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { systemDark = false; }
        IsDark = !HighContrast && (preference == "dark" || preference == "system" && systemDark);
        Colors = Build(IsDark, ReadAccent(IsDark));
        SetAppMode(IsDark);
    }

    private static Palette Build(bool dark, Color? accent)
    {
        if (dark)
        {
            var a = accent ?? Color.FromArgb(0x60, 0xCD, 0xFF);
            var surface = Color.FromArgb(0x20, 0x20, 0x20);
            var input = Color.FromArgb(0x2D, 0x2D, 0x2D);
            return new Palette(surface, input, Color.FromArgb(0x32, 0x32, 0x32), Color.FromArgb(0x27, 0x27, 0x27), input,
                Color.FromArgb(0x3A, 0x3A, 0x3A), Color.FromArgb(0x9A, 0x9A, 0x9A), Color.White, Color.FromArgb(0xC5, 0xC5, 0xC5), Color.FromArgb(0x78, 0x78, 0x78),
                a, Blend(a, surface, 0.1), Blend(a, surface, 0.2), Color.Black,
                Color.FromArgb(0x2C, 0x2C, 0x2C), Color.FromArgb(0x38, 0x38, 0x38), Blend(input, Color.White, 0.05), Blend(input, Color.White, 0.08), Color.FromArgb(0xFF, 0x99, 0xA4));
        }
        else
        {
            var a = accent ?? Color.FromArgb(0x00, 0x5F, 0xB8);
            var surface = Color.FromArgb(0xF3, 0xF3, 0xF3);
            var text = Color.FromArgb(0x1B, 0x1B, 0x1B);
            return new Palette(surface, Color.FromArgb(0xFB, 0xFB, 0xFB), Color.FromArgb(0xF6, 0xF6, 0xF6), Color.FromArgb(0xF0, 0xF0, 0xF0), Color.White,
                Color.FromArgb(0xE0, 0xE0, 0xE0), Color.FromArgb(0x86, 0x86, 0x86), text, Color.FromArgb(0x5E, 0x5E, 0x5E), Color.FromArgb(0xA0, 0xA0, 0xA0),
                a, Blend(a, surface, 0.1), Blend(a, surface, 0.2), Color.White,
                Color.FromArgb(0xF9, 0xF9, 0xF9), Color.FromArgb(0xEA, 0xEA, 0xEA), Blend(Color.White, text, 0.04), Blend(Color.White, text, 0.07), Color.FromArgb(0xC4, 0x2B, 0x1C));
        }
    }

    private static Color? ReadAccent(bool dark)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            // AccentPalette holds eight RGBA shades from lightest to darkest. Fluent uses Light 2 on dark surfaces and Dark 1 on light ones.
            if (key?.GetValue("AccentPalette") is byte[] { Length: >= 32 } shades)
                return dark ? Color.FromArgb(shades[4], shades[5], shades[6]) : Color.FromArgb(shades[16], shades[17], shades[18]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return null;
    }

    public static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (int)Math.Round(from.R + (to.R - from.R) * amount), (int)Math.Round(from.G + (to.G - from.G) * amount), (int)Math.Round(from.B + (to.B - from.B) * amount));

    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics graphics, RectangleF bounds, float radius, Color fill, Color? stroke = null)
    {
        var mode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = RoundedRectangle(bounds, radius))
        using (var brush = new SolidBrush(fill))
        {
            graphics.FillPath(brush, path);
            if (stroke is { } color) { using var pen = new Pen(color); graphics.DrawPath(pen, path); }
        }
        graphics.SmoothingMode = mode;
    }

    public static void DrawCheck(Graphics graphics, RectangleF box, Color color, float width)
    {
        var mode = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawLines(pen, new[]
        {
            new PointF(box.X + box.Width * 0.22f, box.Y + box.Height * 0.52f),
            new PointF(box.X + box.Width * 0.42f, box.Y + box.Height * 0.72f),
            new PointF(box.X + box.Width * 0.78f, box.Y + box.Height * 0.32f),
        });
        graphics.SmoothingMode = mode;
    }

    public static void Paint(Form form)
    {
        var palette = Colors;
        var surface = HighContrast ? SystemColors.Control : palette.Surface;
        var input = HighContrast ? SystemColors.Window : palette.Input;
        var text = HighContrast ? SystemColors.WindowText : palette.Text;
        void Visit(Control control)
        {
            control.ForeColor = text;
            control.BackColor = control is TextBox or ListBox or ComboBox ? input : surface;
            if (control is ComboBox or ListBox or Panel) ApplyNative(control);
            control.Invalidate();
            foreach (Control child in control.Controls) Visit(child);
        }
        Visit(form);
        if (form.IsHandleCreated)
        {
            int value = IsDark ? 1 : 0;
            _ = DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int));
            if (RoundedCorners)
            {
                int caption = HighContrast ? unchecked((int)0xFFFFFFFF) : ColorTranslator.ToWin32(palette.Surface);
                _ = DwmSetWindowAttribute(form.Handle, 35, ref caption, sizeof(int));
            }
        }
    }

    // Native scroll bars and combo boxes only follow the dark theme through these system theme classes.
    public static void ApplyNative(Control control)
    {
        if (!control.IsHandleCreated) return;
        string? name = IsDark ? control is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer" : control is ComboBox ? null : "Explorer";
        _ = SetWindowTheme(control.Handle, name, null);
    }

    public static void PaintMenu(ContextMenuStrip menu)
    {
        menu.Renderer = HighContrast ? ContrastRenderer : FluentRenderer;
        menu.BackColor = HighContrast ? SystemColors.Menu : Colors.Menu;
        menu.ForeColor = HighContrast ? SystemColors.MenuText : Colors.Text;
        int padding = menu.LogicalToDeviceUnits(4);
        menu.Padding = new Padding(0, padding, 0, padding);
        if (menu.IsHandleCreated) StyleMenuWindow(menu);
    }

    public static void StyleMenuWindow(ContextMenuStrip menu)
    {
        if (!RoundedCorners) return;
        int corner = HighContrast ? 0 : 2;
        _ = DwmSetWindowAttribute(menu.Handle, 33, ref corner, sizeof(int));
        int border = HighContrast ? unchecked((int)0xFFFFFFFF) : ColorTranslator.ToWin32(IsDark ? Color.FromArgb(0x45, 0x45, 0x45) : Color.FromArgb(0xD8, 0xD8, 0xD8));
        _ = DwmSetWindowAttribute(menu.Handle, 34, ref border, sizeof(int));
    }

    private sealed class FluentMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Colors.Menu);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (RoundedCorners) return;
            using var pen = new Pen(Colors.Stroke);
            var bounds = e.ToolStrip.ClientRectangle;
            e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            int inset = e.Item.Owner?.LogicalToDeviceUnits(4) ?? 4;
            var bounds = new RectangleF(inset, 0, e.Item.Width - inset * 2 - 1, e.Item.Height - 1);
            FillRounded(e.Graphics, bounds, inset, Colors.MenuHover);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var color = e.Item.Enabled ? Colors.Text : Colors.TextSecondary;
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, color, e.TextFormat);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var bounds = e.ImageRectangle;
            float size = Math.Min(bounds.Width, bounds.Height);
            var box = new RectangleF(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
            DrawCheck(e.Graphics, box, e.Item.Enabled ? Colors.Text : Colors.TextSecondary, Math.Max(1.5f, size / 10));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Colors.Stroke);
            e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Colors.Text;
            base.OnRenderArrow(e);
        }
    }

    private static void SetAppMode(bool dark)
    {
        // Undocumented uxtheme ordinals 135 and 136 switch native popup menus, such as the text box context menu, to the app theme.
        if (Environment.OSVersion.Version.Build < 18362) return;
        try { _ = SetPreferredAppMode(HighContrast ? 0 : dark ? 2 : 3); FlushMenuThemes(); }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { }
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string? subAppName, string? subIdList);
    [DllImport("uxtheme.dll", EntryPoint = "#135")] private static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] private static extern void FlushMenuThemes();
}
