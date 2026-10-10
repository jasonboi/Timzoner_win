namespace Timzoner.Core;

public sealed class AppController
{
    private readonly ITimeZoneService zones;
    private readonly ISettingsStore store;
    private readonly IStartupService startup;
    private readonly HashSet<string> knownIds;
    private Settings settings;
    private int switching;

    public AppController(ITimeZoneService zones, ISettingsStore store, IStartupService startup)
    {
        this.zones = zones;
        this.store = store;
        this.startup = startup;
        Zones = zones.GetZones();
        knownIds = Zones.Select(z => z.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loaded = store.Load(zones.GetCurrentId());
        settings = loaded.Value.Copy();
        AppText.Language = settings.Language;
        Notice = loaded.Notice;
        IsReadOnly = loaded.ReadOnly;
        try { settings = settings with { StartWithWindows = startup.IsEnabled() }; }
        catch (Exception ex) when (IsExpected(ex)) { Notice = Join(Notice, AppText.Get("无法读取开机启动状态，请在保存时重试。", "Unable to read the startup setting. Try again when saving. ") + ex.Message); }
    }

    public IReadOnlyList<Zone> Zones { get; }
    public string? Notice { get; }
    public bool IsReadOnly { get; }
    public bool IsSwitching => Volatile.Read(ref switching) != 0;
    public Settings GetSettings() => settings.Copy();
    public string GetCurrentId() => zones.GetCurrentId();
    public bool? IsAutomaticEnabled() => zones.IsAutomaticEnabled();

    public SaveResult Save(Settings draft)
    {
        if (IsReadOnly) return new(false, AppText.Get("配置来自较新版本，当前版本无法保存。请升级 Timzoner。", "These settings were created by a newer version and cannot be saved. Update Timzoner."));
        if (draft.SchemaVersion != 1 || draft.Theme is not ("system" or "light" or "dark") ||
            draft.Language is not ("zh-CN" or "en") ||
            draft.FavoriteTimeZoneIds is null || draft.FavoriteTimeZoneIds.Any(string.IsNullOrWhiteSpace))
            return new(false, AppText.Get("配置格式无效，请重新打开配置窗口。", "The settings are invalid. Reopen the settings window."));
        if (draft.FavoriteTimeZoneIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != draft.FavoriteTimeZoneIds.Length)
            return new(false, AppText.Get("收藏中包含重复时区，请删除重复项。", "Favorites contain duplicate time zones. Remove the duplicates."));
        if (draft.FavoriteTimeZoneIds.Any(id => !knownIds.Contains(id) && !settings.FavoriteTimeZoneIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
            return new(false, AppText.Get("所选时区已不可用，请刷新后重试。", "A selected time zone is no longer available. Refresh and try again."));

        bool previousStartup;
        try { previousStartup = startup.IsEnabled(); }
        catch (Exception ex) when (IsExpected(ex)) { return new(false, AppText.Get("无法读取开机启动状态。", "Unable to read the startup setting. ") + ex.Message); }
        try
        {
            if (previousStartup != draft.StartWithWindows) startup.SetEnabled(draft.StartWithWindows);
            if (startup.IsEnabled() != draft.StartWithWindows) throw new IOException(AppText.Get("开机启动设置未生效。", "The startup setting was not applied."));
            store.Save(draft.Copy());
            settings = draft.Copy();
            AppText.Language = settings.Language;
            return new(true, AppText.Get("配置已保存。", "Settings saved."));
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            string rollback = "";
            try
            {
                if (startup.IsEnabled() != previousStartup) startup.SetEnabled(previousStartup);
                settings = settings with { StartWithWindows = startup.IsEnabled() };
            }
            catch (Exception restore) when (IsExpected(restore))
            {
                rollback = AppText.Get(" 开机启动状态可能已改变且无法恢复，请检查系统启动应用。", " The startup setting may have changed and could not be restored. Check Windows startup apps.");
            }
            return new(false, AppText.Get("保存失败，原收藏和主题已保留。", "Saving failed. Your previous favorites and appearance settings have been kept. ") + ex.Message + rollback);
        }
    }

    public async Task<SwitchResult> SwitchAsync(string id, bool elevated = false)
    {
        if (Interlocked.CompareExchange(ref switching, 1, 0) != 0)
            return new(SwitchStatus.Busy, AppText.Get("正在切换，请稍候。", "A time zone change is in progress. Please wait."));
        try
        {
            if (!knownIds.Contains(id)) return new(SwitchStatus.InvalidZone, AppText.Get("此时区已不可用，请从收藏中删除。", "This time zone is no longer available. Remove it from favorites."));
            var before = zones.GetCurrentId();
            if (string.Equals(before, id, StringComparison.OrdinalIgnoreCase))
                return new(SwitchStatus.Unchanged, AppText.Get("已经使用此时区。", "This time zone is already in use."), before);
            var result = await zones.SetAsync(id, elevated);
            var actual = zones.GetCurrentId();
            if (result.Status != SwitchStatus.Success) return result with { CurrentId = actual };
            return string.Equals(actual, id, StringComparison.OrdinalIgnoreCase)
                ? new(SwitchStatus.Success, AppText.Get("系统时区已切换。", "The system time zone has been changed."), actual)
                : new(SwitchStatus.Failed, AppText.Get("系统当前时区与所选时区不一致，请检查 Windows 自动时区设置。", "The current system time zone differs from your selection. Check Windows automatic time zone settings."), actual);
        }
        catch (Exception ex) when (IsExpected(ex)) { return new(SwitchStatus.Failed, AppText.Get("无法切换时区。", "Unable to change the time zone. ") + ex.Message); }
        finally { Interlocked.Exchange(ref switching, 0); }
    }

    private static bool IsExpected(Exception ex) => ex is IOException or UnauthorizedAccessException or
        System.ComponentModel.Win32Exception or System.Security.SecurityException or InvalidOperationException;
    private static string Join(string? first, string second) => first is null ? second : first + Environment.NewLine + second;
}
