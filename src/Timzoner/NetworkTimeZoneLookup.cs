using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Timzoner.Core;

namespace Timzoner;

public sealed class NetworkTimeZoneLookup : INetworkTimeZoneLookup, IDisposable
{
    private HttpClient? client;
    private DateTimeOffset retryAfter;

    public async Task<string?> GetTimeZoneIdAsync(CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow < retryAfter)
            throw new HttpRequestException("Time zone lookup is rate limited.", null, HttpStatusCode.TooManyRequests);
        client ??= CreateClient();
        using var response = await client.GetAsync("https://ip-info.com/json",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            retryAfter = response.Headers.RetryAfter?.Date ??
                DateTimeOffset.UtcNow + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromDays(1));
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(16 * 1024, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream,
            new JsonDocumentOptions { MaxDepth = 8 }, cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("timezone", out var id) || id.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(id.GetString()))
            return null;
        return TimeZoneInfo.TryConvertIanaIdToWindowsId(id.GetString()!, out var windowsId) ? windowsId : null;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.Zero,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            MaxConnectionsPerServer = 1
        }) { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Timzoner", Updater.CurrentVersion.ToString(3)));
        return client;
    }

    public void Dispose() => client?.Dispose();
}
