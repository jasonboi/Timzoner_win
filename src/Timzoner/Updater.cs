using System.Net.Http.Headers;
using System.Security.Cryptography;
using Timzoner.Core;

namespace Timzoner;

// Manual update check against GitHub Releases. Networking types load only when the user asks for a check.
internal static class Updater
{
    private const string DownloadPrefix = "https://github.com/jasonboi/Timzoner_win/releases/download/";

    public static Version CurrentVersion => typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static async Task<ReleaseInfo?> GetLatestAsync()
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return UpdateFeed.Parse(await client.GetStringAsync(UpdateFeed.LatestReleaseUrl));
    }

    // Only installers attached to this repository's releases, with a published SHA-256, are downloaded.
    public static bool CanInstall(ReleaseInfo release) =>
        release.InstallerName is not null && release.InstallerSha256 is not null &&
        release.InstallerUrl?.StartsWith(DownloadPrefix, StringComparison.Ordinal) == true;

    public static async Task<string> DownloadAsync(ReleaseInfo release)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Timzoner-update");
        try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.GetFileName(release.InstallerName!));
        using (var client = CreateClient())
        await using (var source = await client.GetStreamAsync(release.InstallerUrl))
        await using (var target = File.Create(path))
            await source.CopyToAsync(target);
        string hash;
        await using (var file = File.OpenRead(path)) hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file));
        if (hash != release.InstallerSha256)
        {
            File.Delete(path);
            throw new InvalidDataException(AppText.Get("安装包校验失败。", "The installer checksum does not match."));
        }
        return path;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Timzoner", CurrentVersion.ToString(3)));
        return client;
    }
}
