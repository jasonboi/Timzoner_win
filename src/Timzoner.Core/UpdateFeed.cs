using System.Text.Json;

namespace Timzoner.Core;

public sealed record ReleaseInfo(Version Version, string PageUrl, string? InstallerName, string? InstallerUrl, string? InstallerSha256);

public static class UpdateFeed
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/jasonboi/Timzoner_win/releases/latest";
    private const string InstallerSuffix = "-win-x64-setup.exe";

    // Reads the GitHub "latest release" response. Returns null when the tag is not a version.
    public static ReleaseInfo? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        var version = ParseVersion(Text(root, "tag_name"));
        if (version is null) return null;
        string page = Text(root, "html_url") ?? "";
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = Text(asset, "name");
                if (name is null || !name.EndsWith(InstallerSuffix, StringComparison.OrdinalIgnoreCase)) continue;
                var digest = Text(asset, "digest");
                string? sha256 = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) && digest.Length == 71 ? digest[7..].ToLowerInvariant() : null;
                return new ReleaseInfo(version, page, name, Text(asset, "browser_download_url"), sha256);
            }
        }
        return new ReleaseInfo(version, page, null, null, null);
    }

    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = text.Trim().TrimStart('v', 'V');
        int metadata = value.IndexOfAny(['+', '-']);
        if (metadata >= 0) value = value[..metadata];
        return Version.TryParse(value, out var version) ? Normalize(version) : null;
    }

    public static bool IsNewer(ReleaseInfo release, Version current) => release.Version > Normalize(current);

    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
