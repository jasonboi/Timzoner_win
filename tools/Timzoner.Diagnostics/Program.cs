using System.Text.Json;
using Timzoner;
using Timzoner.Core;

if (args.Length != 1 || args[0] is not ("--read" or "--verify-switch")) return 2;
var service = new WindowsTimeZoneService();
var original = WindowsTimeZoneService.GetSnapshot();
if (args[0] == "--read")
{
    Console.WriteLine(JsonSerializer.Serialize(new { original.Id, original.DaylightDisabled, Automatic = service.IsAutomaticEnabled(), ZoneCount = service.GetZones().Count }));
    return 0;
}

// This mode is explicitly opt-in and intended only for an authorized acceptance run.
string target = original.Id == "UTC" ? "China Standard Time" : "UTC";
SwitchResult? switched = null;
SwitchResult? restored = null;
string? actual = null;
try
{
    var controller = new AppController(service, new MemoryStore(), new NoStartup());
    switched = await controller.SwitchAsync(target);
    actual = service.GetCurrentId();
}
finally
{
    restored = WindowsTimeZoneService.SetDirect(original.Id, original.DaylightDisabled);
    var after = WindowsTimeZoneService.GetSnapshot();
    var report = new
    {
        TimestampUtc = DateTime.UtcNow,
        Original = original.Id, OriginalDaylightDisabled = original.DaylightDisabled,
        Target = target, Switch = switched, Readback = actual, Restore = restored,
        Restored = after.Id == original.Id && after.DaylightDisabled == original.DaylightDisabled,
        Final = after.Id, FinalDaylightDisabled = after.DaylightDisabled
    };
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}
return switched?.Status == SwitchStatus.Success && actual == target && restored?.Status == SwitchStatus.Success && WindowsTimeZoneService.GetSnapshot() == original ? 0 : 1;

sealed class MemoryStore : ISettingsStore
{
    public LoadedSettings Load(string id) => new(new Settings { FavoriteTimeZoneIds = [id] });
    public void Save(Settings settings) { }
}
sealed class NoStartup : IStartupService
{
    public bool IsEnabled() => false;
    public void SetEnabled(bool enabled) { }
}
