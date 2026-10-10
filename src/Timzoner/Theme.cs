using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Timzoner;

internal static class Theme
{
    public static bool IsDark { get; private set; }
    public static void Apply(string preference)
    {
        bool systemDark;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            systemDark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { systemDark = false; }
        IsDark = !SystemInformation.HighContrast && (preference == "dark" || preference == "system" && systemDark);
    }

    public static void Paint(Form form)
    {
        var dark = IsDark;
        var surface = SystemInformation.HighContrast ? SystemColors.Control : dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(248, 248, 248);
        var input = SystemInformation.HighContrast ? SystemColors.Window : dark ? Color.FromArgb(45, 45, 45) : Color.White;
        var text = SystemInformation.HighContrast ? SystemColors.WindowText : dark ? Color.FromArgb(242, 242, 242) : Color.FromArgb(30, 30, 30);
        void Visit(Control control)
        {
            control.ForeColor = text;
            control.BackColor = control is TextBox or ListBox or ComboBox ? input : surface;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.FlatAppearance.BorderColor = SystemInformation.HighContrast ? SystemColors.WindowFrame : dark ? Color.FromArgb(105, 105, 105) : Color.FromArgb(170, 170, 170);
            }
            if (control is CheckBox check) check.FlatStyle = SystemInformation.HighContrast ? FlatStyle.Standard : FlatStyle.Flat;
            if (control is ComboBox combo) combo.FlatStyle = SystemInformation.HighContrast ? FlatStyle.Standard : FlatStyle.Flat;
            foreach (Control child in control.Controls) Visit(child);
        }
        Visit(form);
        if (form.IsHandleCreated)
        {
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int));
        }
    }

    public static void PaintMenu(ContextMenuStrip menu)
    {
        menu.Renderer = IsDark && !SystemInformation.HighContrast ? new ToolStripProfessionalRenderer(new DarkMenuColors()) : new ToolStripProfessionalRenderer();
        menu.BackColor = IsDark ? Color.FromArgb(32, 32, 32) : SystemColors.Menu;
        menu.ForeColor = IsDark ? Color.WhiteSmoke : SystemColors.MenuText;
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 32);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color MenuItemSelected => Color.FromArgb(64, 64, 64);
        public override Color MenuItemBorder => Color.FromArgb(110, 110, 110);
        public override Color MenuBorder => Color.FromArgb(110, 110, 110);
        public override Color CheckBackground => Color.FromArgb(64, 64, 64);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
