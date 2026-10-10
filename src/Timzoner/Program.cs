using System.Security.Principal;
using Timzoner.Core;

namespace Timzoner;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--elevated-switch")
        {
            var result = WindowsTimeZoneService.SetDirect(args[1]);
            return result.Status == SwitchStatus.Success ? 0 : result.Status == SwitchStatus.PermissionDenied ? 1314 : 1;
        }
        if (args.Any(arg => arg is not ("--startup" or "--settings" or "--exit"))) return 2;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("Microsoft YaHei UI", 9F));
        var testDirectory = Environment.GetEnvironmentVariable("TIMZONER_TEST_DATA");
        if (!string.IsNullOrWhiteSpace(testDirectory)) testDirectory = Path.GetFullPath(testDirectory);
        var suffix = WindowsIdentity.GetCurrent().User!.Value + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
        if (!string.IsNullOrWhiteSpace(testDirectory))
            suffix += "-test-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(testDirectory)))[..16];
        using var mutex = new Mutex(true, @"Local\Timzoner-" + suffix, out bool first);
        using var activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Timzoner-activate-" + suffix);
        using var shutdown = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Timzoner-exit-" + suffix);
        if (!first)
        {
            if (args.Contains("--exit"))
            {
                shutdown.Set();
                try { if (!mutex.WaitOne(TimeSpan.FromSeconds(15))) return 3; }
                catch (AbandonedMutexException) { }
                mutex.ReleaseMutex();
            }
            else if (!args.Contains("--startup")) activation.Set();
            return 0;
        }
        if (args.Contains("--exit")) { mutex.ReleaseMutex(); return 0; }
        try
        {
            var path = string.IsNullOrWhiteSpace(testDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Timzoner", "settings.json")
                : Path.Combine(testDirectory, "settings.json");
            var controller = new AppController(new WindowsTimeZoneService(), new JsonSettingsStore(path),
                string.IsNullOrWhiteSpace(testDirectory) ? new StartupService() : new TestStartupService());
            Theme.Apply(controller.GetSettings().Theme);
            using var context = new TrayContext(controller, activation, shutdown, args.Contains("--settings"));
            Application.Run(context);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(AppText.Get("Timzoner 无法启动。", "Timzoner could not start.") + Environment.NewLine + ex.Message, "Timzoner", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally { mutex.ReleaseMutex(); }
    }

    private sealed class TestStartupService : IStartupService
    {
        private bool enabled;
        public bool IsEnabled() => enabled;
        public void SetEnabled(bool value) => enabled = value;
    }
}
