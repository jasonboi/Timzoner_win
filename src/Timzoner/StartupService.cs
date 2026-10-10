using Microsoft.Win32;
using Timzoner.Core;

namespace Timzoner;

public sealed class StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string Command => "\"" + Environment.ProcessPath + "\" --startup";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue("Timzoner") as string, Command, StringComparison.OrdinalIgnoreCase);
    }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue("Timzoner", Command, RegistryValueKind.String);
        else key.DeleteValue("Timzoner", false);
    }
}
