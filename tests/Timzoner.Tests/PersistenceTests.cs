using Timzoner.Core;
using Xunit;

namespace Timzoner.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Timzoner-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(directory, "settings.json");
    public PersistenceTests() => Directory.CreateDirectory(directory);
    [Fact] public void FirstRunIsPersistedAndBackupRecoversCorruption()
    {
        var store = new JsonSettingsStore(FilePath);
        Assert.Equal("A", store.Load("A").Value.FavoriteTimeZoneIds[0]);
        store.Save(new Settings { FavoriteTimeZoneIds = ["B"], Theme = "dark" });
        File.WriteAllText(FilePath, "{broken");
        var recovered = store.Load("C");
        Assert.False(recovered.ReadOnly); Assert.NotNull(recovered.Notice);
        Assert.Equal("A", recovered.Value.FavoriteTimeZoneIds[0]);
        Assert.Single(Directory.GetFiles(directory, "*.corrupt-*"));
        Assert.Equal("A", store.Load("C").Value.FavoriteTimeZoneIds[0]);
    }
    [Fact] public void FutureVersionBytesArePreserved()
    {
        const string future = "{\"schemaVersion\":99,\"somethingNew\":true}";
        File.WriteAllText(FilePath, future);
        Assert.True(new JsonSettingsStore(FilePath).Load("A").ReadOnly);
        Assert.Equal(future, File.ReadAllText(FilePath));
    }
    [Fact] public void EnglishLanguageSurvivesFileRoundTrip()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Save(new Settings { FavoriteTimeZoneIds = ["A"], Language = "en" });
        Assert.Equal("en", new JsonSettingsStore(FilePath).Load("B").Value.Language);
    }
    [Fact] public void EnglishBackupRecoveryUsesTheRestoredLanguageForNotice()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Save(new Settings { FavoriteTimeZoneIds = ["A"], Language = "en" });
        store.Save(new Settings { FavoriteTimeZoneIds = ["B"], Language = "zh-CN" });
        File.WriteAllText(FilePath, "{broken");
        var recovered = store.Load("C");
        Assert.False(recovered.ReadOnly);
        Assert.Equal("en", recovered.Value.Language);
        Assert.Equal(new[] { "A" }, recovered.Value.FavoriteTimeZoneIds);
        Assert.Equal("The settings file was damaged. The original file was kept and settings were restored from a backup.", recovered.Notice);
        Assert.Equal("en", store.Load("C").Value.Language);
    }
    [Fact] public void ConfigurationWithoutLanguageDefaultsToChinese()
    {
        const string legacy = "{\"schemaVersion\":1,\"favoriteTimeZoneIds\":[\"B\"],\"theme\":\"dark\"}";
        File.WriteAllText(FilePath, legacy);
        var loaded = new JsonSettingsStore(FilePath).Load("A");
        Assert.Null(loaded.Notice);
        Assert.Equal("zh-CN", loaded.Value.Language);
        Assert.Equal(new[] { "B" }, loaded.Value.FavoriteTimeZoneIds);
        Assert.Equal("dark", loaded.Value.Theme);
        Assert.Equal(legacy, File.ReadAllText(FilePath));
    }
    [Theory] [InlineData("\"fr\"")] [InlineData("null")] [InlineData("42")]
    public void InvalidLanguageInFileRecoversAndPreservesOriginal(string language)
    {
        var content = "{\"schemaVersion\":1,\"favoriteTimeZoneIds\":[\"B\"],\"language\":" + language + "}";
        File.WriteAllText(FilePath, content);
        var loaded = new JsonSettingsStore(FilePath).Load("A");
        Assert.False(loaded.ReadOnly);
        Assert.NotNull(loaded.Notice);
        Assert.Equal("zh-CN", loaded.Value.Language);
        Assert.Equal(new[] { "A" }, loaded.Value.FavoriteTimeZoneIds);
        Assert.Equal(content, File.ReadAllText(Directory.GetFiles(directory, "*.corrupt-*").Single()));
    }
    [Theory] [InlineData("null")] [InlineData("[]")] [InlineData("{\"schemaVersion\":1,\"favoriteTimeZoneIds\":null}")]
    [InlineData("{\"schemaVersion\":\"1\"}")] [InlineData("{\"schemaVersion\":null}")]
    [InlineData("{\"schemaVersion\":true}")] [InlineData("{\"schemaVersion\":[]}")] [InlineData("{\"schemaVersion\":{}}")]
    public void InvalidConfigurationRecoversWithoutLosingOriginal(string content)
    {
        File.WriteAllText(FilePath, content);
        var loaded = new JsonSettingsStore(FilePath).Load("A");
        Assert.Equal("A", loaded.Value.FavoriteTimeZoneIds[0]);
        Assert.Equal(content, File.ReadAllText(Directory.GetFiles(directory, "*.corrupt-*").Single()));
    }
    [Theory] [InlineData("[]")] [InlineData("{\"schemaVersion\":0}")]
    public void CorruptPrimaryAndInvalidBackupRecoverToWritableDefaults(string backup)
    {
        File.WriteAllText(FilePath, "broken"); File.WriteAllText(FilePath + ".bak", backup);
        var loaded = new JsonSettingsStore(FilePath).Load("A");
        Assert.False(loaded.ReadOnly); Assert.Equal("A", loaded.Value.FavoriteTimeZoneIds[0]);
        Assert.Equal(backup, File.ReadAllText(Directory.GetFiles(directory, "*.bak.corrupt-*").Single()));
    }
    public void Dispose() => Directory.Delete(directory, true);
}
