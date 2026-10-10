using Timzoner.Core;
using Xunit;

namespace Timzoner.Tests;

public sealed class BehaviorTests
{
    [Fact] public async Task SwitchVerifiesSystemReadback()
    {
        var zones = new Zones(); var controller = Create(zones);
        Assert.Equal(SwitchStatus.Success, (await controller.SwitchAsync("B")).Status);
        Assert.Equal("B", controller.GetCurrentId());
        Assert.Equal(SwitchStatus.Unchanged, (await controller.SwitchAsync("B")).Status);
        Assert.Equal(1, zones.Calls);
    }
    [Fact] public async Task ClaimedSuccessWithoutReadbackIsFailure()
    {
        var zones = new Zones { Apply = false };
        Assert.Equal(SwitchStatus.Failed, (await Create(zones).SwitchAsync("B")).Status);
    }
    [Theory]
    [InlineData(SwitchStatus.PermissionDenied)] [InlineData(SwitchStatus.Cancelled)] [InlineData(SwitchStatus.Failed)]
    public async Task FailuresPreserveRealCurrentState(SwitchStatus status)
    {
        var result = await Create(new Zones { Outcome = status }).SwitchAsync("B", true);
        Assert.Equal(status, result.Status); Assert.Equal("A", result.CurrentId);
    }
    [Fact] public async Task UnknownZoneNeverInvokesSystem()
    {
        var zones = new Zones();
        Assert.Equal(SwitchStatus.InvalidZone, (await Create(zones).SwitchAsync("missing")).Status);
        Assert.Equal(0, zones.Calls);
    }
    [Fact] public async Task ConcurrentRequestsAreSerialized()
    {
        var zones = new Zones { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var controller = Create(zones); var first = controller.SwitchAsync("B");
        Assert.Equal(SwitchStatus.Busy, (await controller.SwitchAsync("B")).Status);
        zones.Gate.SetResult(); await first;
        Assert.False(controller.IsSwitching); Assert.Equal(1, zones.Calls);
    }
    [Fact] public void DraftIsIsolatedAndSavePersistsOrderingAndTheme()
    {
        var store = new Store(); var controller = Create(store: store);
        var draft = controller.GetSettings(); draft.FavoriteTimeZoneIds[0] = "B";
        Assert.Equal("A", controller.GetSettings().FavoriteTimeZoneIds[0]);
        Assert.True(controller.Save(draft with { FavoriteTimeZoneIds = ["B", "A"], Theme = "dark" }).Success);
        var restarted = Create(store: store);
        Assert.Equal(new[] { "B", "A" }, restarted.GetSettings().FavoriteTimeZoneIds);
        Assert.Equal("dark", restarted.GetSettings().Theme);
    }
    [Fact] public void EnglishLanguageIsSavedAndReloaded()
    {
        var store = new Store(); var controller = Create(store: store);
        Assert.True(controller.Save(controller.GetSettings() with { Language = "en" }).Success);
        Assert.Equal("en", store.Value.Language);
        Assert.Equal("en", Create(store: store).GetSettings().Language);
    }
    [Theory] [InlineData("fr")] [InlineData("EN")] [InlineData("")] [InlineData(null)]
    public void InvalidLanguageDoesNotPersistOrChangeCommittedSettings(string? language)
    {
        var store = new Store { Value = new Settings { FavoriteTimeZoneIds = ["A"], Language = "en" } };
        var startup = new Startup(); var controller = Create(store: store, startup: startup);
        Assert.False(controller.Save(controller.GetSettings() with
        {
            FavoriteTimeZoneIds = ["B"], Language = language!, StartWithWindows = true
        }).Success);
        Assert.Equal("en", controller.GetSettings().Language);
        Assert.Equal("en", store.Value.Language);
        Assert.Equal(new[] { "A" }, store.Value.FavoriteTimeZoneIds);
        Assert.False(startup.Enabled);
    }
    [Fact] public void DuplicateFavoritesRejectedAndEmptyFavoritesAllowed()
    {
        var controller = Create();
        Assert.False(controller.Save(new Settings { FavoriteTimeZoneIds = ["A", "a"] }).Success);
        Assert.True(controller.Save(new Settings()).Success);
        Assert.Empty(controller.GetSettings().FavoriteTimeZoneIds);
    }
    [Fact] public void FailedSaveRollsBackStartupAndRetainsCommittedSettings()
    {
        var startup = new Startup(); var controller = Create(store: new Store { Fail = true }, startup: startup);
        Assert.False(controller.Save(new Settings { FavoriteTimeZoneIds = ["B"], StartWithWindows = true }).Success);
        Assert.False(startup.Enabled); Assert.Equal("A", controller.GetSettings().FavoriteTimeZoneIds[0]);
    }
    [Fact] public void StartupFailureDoesNotPersistDraft()
    {
        var store = new Store(); var controller = Create(store: store, startup: new Startup { Fail = true });
        Assert.False(controller.Save(new Settings { StartWithWindows = true }).Success);
        Assert.Equal("A", store.Value.FavoriteTimeZoneIds[0]);
    }
    [Fact] public void NewerConfigurationCannotBeOverwritten()
    {
        Assert.False(Create(store: new Store { ReadOnly = true }).Save(new Settings()).Success);
    }
    [Fact] public void ExternalChangesAreReadFromSystem()
    {
        var zones = new Zones(); var controller = Create(zones); zones.Current = "B";
        Assert.Equal("B", controller.GetCurrentId());
    }

    private static AppController Create(Zones? zones = null, Store? store = null, Startup? startup = null) => new(zones ?? new(), store ?? new(), startup ?? new());
    private sealed class Zones : ITimeZoneService
    {
        public string Current = "A";
        public bool Apply = true;
        public int Calls;
        public SwitchStatus Outcome = SwitchStatus.Success;
        public TaskCompletionSource? Gate;
        public IReadOnlyList<Zone> GetZones() => [new("A", "Alpha"), new("B", "Beta")];
        public string GetCurrentId() => Current;
        public bool? IsAutomaticEnabled() => null;
        public async Task<SwitchResult> SetAsync(string id, bool elevated)
        {
            Calls++; if (Gate is not null) await Gate.Task;
            if (Apply && Outcome == SwitchStatus.Success) Current = id;
            return new(Outcome, "result");
        }
    }
    private sealed class Store : ISettingsStore
    {
        public Settings Value = new() { FavoriteTimeZoneIds = ["A"] };
        public bool Fail, ReadOnly;
        public LoadedSettings Load(string currentId) => new(Value, ReadOnly: ReadOnly);
        public void Save(Settings settings) { if (Fail) throw new IOException("disk"); Value = settings; }
    }
    private sealed class Startup : IStartupService
    {
        public bool Enabled, Fail;
        public bool IsEnabled() => Enabled;
        public void SetEnabled(bool enabled) { if (Fail) throw new UnauthorizedAccessException(); Enabled = enabled; }
    }
}
