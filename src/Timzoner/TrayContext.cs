using Microsoft.Win32;
using System.Net.NetworkInformation;
using Timzoner.Core;

namespace Timzoner;

internal sealed class TrayContext : ApplicationContext
{
    private readonly AppController controller;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly Form dispatcher = new() { ShowInTaskbar = false, Text = "Timzoner message window" };
    private readonly RegisteredWaitHandle activationWait;
    private readonly RegisteredWaitHandle shutdownWait;
    private readonly Icon appIcon;
    private readonly Icon trayIcon;
    private readonly NetworkTimeZoneLookup networkLookup = new();
    private readonly NetworkTimeZoneSwitcher automaticSwitcher;
    private bool networkSubscribed;
    private SettingsForm? settingsForm;
    private bool disposed;
    private bool checkingUpdate;
    private static string T(string chinese, string english) => AppText.Get(chinese, english);
    private static string ZoneName(Zone zone) => AppText.IsEnglish ? zone.EnglishName : zone.Name;

    public TrayContext(AppController controller, EventWaitHandle activation, EventWaitHandle shutdown, bool showSettings)
    {
        this.controller = controller;
        automaticSwitcher = new NetworkTimeZoneSwitcher(controller, networkLookup, result =>
        {
            if (disposed) return;
            RefreshMenu();
            if (result.Status is not (SwitchStatus.Success or SwitchStatus.Unchanged))
                Notify(result.Message, ToolTipIcon.Warning);
        });
        _ = dispatcher.Handle;
        dispatcher.FormClosed += (_, _) => ExitThread();
        appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
        trayIcon = Icon.ExtractIcon(Application.ExecutablePath, 0, SystemInformation.SmallIconSize.Width) ?? (Icon)appIcon.Clone();
        menu.HandleCreated += (_, _) => Theme.StyleMenuWindow(menu);
        menu.ItemAdded += (_, e) => { if (e.Item is ToolStripMenuItem) e.Item.Padding = new Padding(menu.LogicalToDeviceUnits(8), menu.LogicalToDeviceUnits(3), 0, menu.LogicalToDeviceUnits(3)); };
        tray = new NotifyIcon { Icon = trayIcon, Text = "Timzoner", ContextMenuStrip = menu, Visible = true };
        menu.Opening += (_, _) => RefreshMenu();
        tray.DoubleClick += (_, _) => OpenSettings();
        activationWait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => Dispatch(OpenSettings), null, Timeout.Infinite, false);
        shutdownWait = ThreadPool.RegisterWaitForSingleObject(shutdown, (_, _) => Dispatch(() => { if (!controller.IsSwitching) ExitThread(); }), null, Timeout.Infinite, false);
        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        RefreshMenu();
        if (controller.Notice is { } notice) tray.ShowBalloonTip(5000, "Timzoner", notice, ToolTipIcon.Warning);
        if (showSettings) dispatcher.BeginInvoke(OpenSettings);
        dispatcher.BeginInvoke(UpdateAutomaticMode);
    }

    private void OnTimeChanged(object? sender, EventArgs e) => Dispatch(RefreshMenu);
    private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatch(() =>
    {
        if (settingsForm is null) { Theme.Apply(controller.GetSettings().Theme); Theme.PaintMenu(menu); }
    });
    private void Dispatch(Action action)
    {
        if (!disposed && dispatcher.IsHandleCreated)
        {
            try { dispatcher.BeginInvoke(() => { if (!disposed) action(); }); }
            catch (InvalidOperationException) { }
        }
    }

    private void RefreshMenu()
    {
        Theme.PaintMenu(menu);
        foreach (ToolStripItem item in menu.Items.Cast<ToolStripItem>().ToArray()) item.Dispose();
        menu.Items.Clear();
        string current;
        try { current = controller.GetCurrentId(); }
        catch (Exception ex) { menu.Items.Add(T("无法读取当前时区 · ", "Unable to read current time zone · ") + ex.Message).Enabled = false; AddCommands(); return; }
        var currentZone = controller.Zones.FirstOrDefault(z => z.Id == current);
        var name = currentZone is null ? current : ZoneName(currentZone);
        menu.Items.Add(T("当前 · ", "Current · ") + name).Enabled = false;
        tray.Text = ("Timzoner · " + current)[..Math.Min(63, ("Timzoner · " + current).Length)];
        menu.Items.Add(new ToolStripSeparator());
        foreach (var id in controller.GetSettings().FavoriteTimeZoneIds)
        {
            var zone = controller.Zones.FirstOrDefault(z => string.Equals(z.Id, id, StringComparison.OrdinalIgnoreCase));
            var item = new ToolStripMenuItem(zone is null ? id + T("（不可用）", " (unavailable)") : ZoneName(zone))
            {
                Checked = string.Equals(id, current, StringComparison.OrdinalIgnoreCase),
                Enabled = zone is not null && !controller.IsSwitching
            };
            item.Click += async (_, _) => await SwitchAsync(id);
            menu.Items.Add(item);
        }
        if (controller.GetSettings().FavoriteTimeZoneIds.Length == 0) menu.Items.Add(T("尚未收藏时区", "No favorite time zones yet")).Enabled = false;
        AddCommands();
    }

    private void AddCommands()
    {
        menu.Items.Add(new ToolStripSeparator());
        var automatic = new ToolStripMenuItem(T("自动切换时区", "Automatic time zone"))
        {
            Name = "AutomaticTimeZone",
            Checked = controller.GetSettings().AutomaticTimeZone,
            Enabled = !controller.IsReadOnly
        };
        automatic.Click += (_, _) => ToggleAutomaticMode();
        menu.Items.Add(automatic);
        menu.Items.Add(T("配置…", "Settings…"), null, (_, _) => OpenSettings());
        menu.Items.Add(checkingUpdate ? T("正在检查更新…", "Checking for updates…") : T("检查更新…", "Check for updates…"), null, async (_, _) => await CheckUpdatesAsync()).Enabled = !checkingUpdate;
        menu.Items.Add(T("Windows 日期和时间设置", "Windows date and time settings"), null, (_, _) => OpenSystemSettings());
        menu.Items.Add(T("退出", "Exit"), null, (_, _) => { if (!controller.IsSwitching) ExitThread(); }).Enabled = !controller.IsSwitching;
    }

    private async Task SwitchAsync(string id)
    {
        automaticSwitcher.CancelPending();
        var operation = controller.SwitchAsync(id);
        RefreshMenu();
        var result = await operation;
        if (result.Status == SwitchStatus.PermissionDenied && MessageBox.Show(result.Message + T("\n是否授权重试？", "\nRetry with administrator permission?"), T("切换时区", "Switch time zone"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            operation = controller.SwitchAsync(id, true);
            RefreshMenu();
            result = await operation;
        }
        if (disposed) return;
        RefreshMenu();
        var automatic = controller.IsAutomaticEnabled();
        var message = result.Message;
        if (automatic == true) message += T(" Windows 自动时区已开启，可能覆盖本次选择，可从菜单打开系统设置。", " Automatic time zone is enabled in Windows and may override this choice. Open Windows settings from the tray menu to review it.");
        tray.ShowBalloonTip(5000, "Timzoner", message, result.Status is SwitchStatus.Success or SwitchStatus.Unchanged ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    private void ToggleAutomaticMode()
    {
        var settings = controller.GetSettings();
        var previewLanguage = AppText.Language;
        var result = controller.Save(settings with { AutomaticTimeZone = !settings.AutomaticTimeZone });
        AppText.Language = previewLanguage;
        if (!result.Success) Notify(result.Message, ToolTipIcon.Warning);
        else UpdateAutomaticMode();
        RefreshMenu();
    }

    private void UpdateAutomaticMode()
    {
        var enabled = controller.GetSettings().AutomaticTimeZone;
        if (enabled && !networkSubscribed)
        {
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            networkSubscribed = true;
        }
        else if (!enabled) UnsubscribeNetwork();
        automaticSwitcher.NetworkChanged(IsNetworkAvailable());
        automaticSwitcher.SetEnabled(enabled);
    }

    private static bool IsNetworkAvailable()
    {
        try { return NetworkInterface.GetIsNetworkAvailable(); }
        catch (NetworkInformationException) { return false; }
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => Dispatch(() => automaticSwitcher.NetworkChanged(IsNetworkAvailable()));
    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => OnNetworkChanged(sender, e);
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) OnNetworkChanged(sender, EventArgs.Empty);
    }

    private void UnsubscribeNetwork()
    {
        if (!networkSubscribed) return;
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        networkSubscribed = false;
    }

    private async Task CheckUpdatesAsync()
    {
        if (checkingUpdate) return;
        checkingUpdate = true;
        try
        {
            ReleaseInfo? release;
            try { release = await Updater.GetLatestAsync(); }
            catch (Exception ex) when (IsNetworkError(ex)) { Notify(T("无法检查更新。", "Unable to check for updates. ") + ex.Message, ToolTipIcon.Warning); return; }
            if (disposed) return;
            if (release is null) { Notify(T("无法识别最新版本信息。", "The latest release information could not be read."), ToolTipIcon.Warning); return; }
            var current = Updater.CurrentVersion.ToString(3);
            if (!UpdateFeed.IsNewer(release, Updater.CurrentVersion)) { Notify(T($"已是最新版本 {current}。", $"Timzoner {current} is up to date."), ToolTipIcon.Info); return; }
            var latest = release.Version.ToString(3);
            if (!Updater.CanInstall(release))
            {
                if (MessageBox.Show(T($"发现新版本 {latest}，但没有可校验的安装包。是否打开下载页面？", $"Version {latest} is available, but no verifiable installer was found. Open the download page?"),
                    "Timzoner", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) OpenUrl(release.PageUrl);
                return;
            }
            if (MessageBox.Show(T($"发现新版本 {latest}，当前版本 {current}。\n是否下载并安装？安装期间 Timzoner 会退出，完成后自动重新启动。",
                    $"Version {latest} is available. You have {current}.\nDownload and install it now? Timzoner closes during the update and restarts afterward."),
                "Timzoner", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string installer;
            try { installer = await Updater.DownloadAsync(release); }
            catch (Exception ex) when (IsNetworkError(ex)) { Notify(T("下载更新失败。", "Unable to download the update. ") + ex.Message, ToolTipIcon.Warning); return; }
            if (disposed) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installer, "/SILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException) { Notify(T("无法启动安装程序。", "Unable to start the installer. ") + ex.Message, ToolTipIcon.Warning); }
        }
        finally { checkingUpdate = false; }
    }

    private static bool IsNetworkError(Exception ex) => ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException;
    private void Notify(string message, ToolTipIcon icon) { if (!disposed) tray.ShowBalloonTip(5000, "Timzoner", message, icon); }
    private static void OpenUrl(string url)
    {
        if (!url.StartsWith("https://github.com/", StringComparison.Ordinal)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception) { }
    }

    private void OpenSettings()
    {
        if (settingsForm is null || settingsForm.IsDisposed)
        {
            settingsForm = new SettingsForm(controller, RefreshMenu, appIcon);
            settingsForm.FormClosed += (_, _) => settingsForm = null;
            settingsForm.Show();
        }
        else
        {
            if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Show(); settingsForm.Activate();
        }
    }
    internal static void OpenSystemSettings()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:dateandtime") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(T("无法打开系统设置。", "Unable to open Windows settings. ") + ex.Message, "Timzoner"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            UnsubscribeNetwork();
            automaticSwitcher.Dispose();
            networkLookup.Dispose();
            SystemEvents.TimeChanged -= OnTimeChanged;
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
            activationWait.Unregister(null);
            shutdownWait.Unregister(null);
            tray.Visible = false;
            tray.Dispose();
            settingsForm?.Dispose();
            appIcon.Dispose();
            trayIcon.Dispose();
            menu.Dispose();
            dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
