namespace Timzoner.Core;

public sealed record Zone(string Id, string Name, TimeSpan BaseUtcOffset = default)
{
    public override string ToString() => Name;
    public string EnglishName
    {
        get
        {
            string offset = BaseUtcOffset == TimeSpan.Zero ? "UTC" : "UTC" +
                (BaseUtcOffset < TimeSpan.Zero ? "-" : "+") + BaseUtcOffset.Duration().ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
            return $"({offset}) {Id}";
        }
    }
}

public sealed record Settings
{
    public int SchemaVersion { get; init; } = 1;
    public string[] FavoriteTimeZoneIds { get; init; } = [];
    public string Theme { get; init; } = "system";
    public string Language { get; init; } = "zh-CN";
    public bool StartWithWindows { get; init; }
    public bool AutomaticTimeZone { get; init; }
    public Settings Copy() => this with { FavoriteTimeZoneIds = [.. FavoriteTimeZoneIds] };
}

public sealed record LoadedSettings(Settings Value, string? Notice = null, bool ReadOnly = false);
public enum SwitchStatus { Success, Unchanged, Busy, InvalidZone, PermissionDenied, Cancelled, Failed }
public sealed record SwitchResult(SwitchStatus Status, string Message, string? CurrentId = null);
public sealed record SaveResult(bool Success, string Message);

public interface ITimeZoneService
{
    IReadOnlyList<Zone> GetZones();
    string GetCurrentId();
    bool? IsAutomaticEnabled();
    Task<SwitchResult> SetAsync(string id, bool elevated);
}

public interface ISettingsStore
{
    LoadedSettings Load(string currentId);
    void Save(Settings settings);
}

public interface IStartupService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public interface INetworkTimeZoneLookup
{
    Task<string?> GetTimeZoneIdAsync(CancellationToken cancellationToken);
}
