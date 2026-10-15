using System.Text.Json;

namespace Timzoner.Core;

public sealed record NetworkTimeZoneOptions
{
    public TimeSpan DebounceDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan MinimumQueryInterval { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan QueryTimeout { get; init; } = TimeSpan.FromSeconds(15);
}

// Call scheduling methods from the UI synchronization context. Awaited work resumes there before changing settings or notifying the UI.
public sealed class NetworkTimeZoneSwitcher : IDisposable
{
    private readonly AppController controller;
    private readonly INetworkTimeZoneLookup lookup;
    private readonly Action<SwitchResult> completed;
    private readonly TimeProvider clock;
    private readonly NetworkTimeZoneOptions options;
    private CancellationTokenSource? pending;
    private long? lastQuery;
    private bool enabled;
    private bool available = true;
    private bool disposed;

    public NetworkTimeZoneSwitcher(AppController controller, INetworkTimeZoneLookup lookup, Action<SwitchResult> completed,
        TimeProvider? timeProvider = null, NetworkTimeZoneOptions? options = null)
    {
        this.controller = controller;
        this.lookup = lookup;
        this.completed = completed;
        clock = timeProvider ?? TimeProvider.System;
        this.options = options ?? new();
        if (this.options.DebounceDelay < TimeSpan.Zero || this.options.MinimumQueryInterval < TimeSpan.Zero ||
            this.options.RetryDelay < TimeSpan.Zero || this.options.QueryTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
    }

    public void SetEnabled(bool value)
    {
        if (disposed || enabled == value) return;
        enabled = value;
        CancelPending();
        if (enabled && available) Schedule();
    }

    public void NetworkChanged(bool available)
    {
        if (disposed) return;
        this.available = available;
        CancelPending();
        if (enabled && available) Schedule();
    }

    public void CancelPending()
    {
        var previous = pending;
        pending = null;
        previous?.Cancel();
    }

    private void Schedule()
    {
        var round = new CancellationTokenSource();
        pending = round;
        _ = RunAsync(round);
    }

    private bool IsCurrent(CancellationTokenSource round) => !disposed && enabled && available &&
        ReferenceEquals(pending, round) && !round.IsCancellationRequested;

    private async Task RunAsync(CancellationTokenSource round)
    {
        try
        {
            await Task.Delay(options.DebounceDelay, clock, round.Token);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await WaitForQueryIntervalAsync(round.Token);
                if (!IsCurrent(round)) return;
                lastQuery = clock.GetTimestamp();
                string? id;
                try
                {
                    using var query = CancellationTokenSource.CreateLinkedTokenSource(round.Token);
                    try
                    {
                        id = await lookup.GetTimeZoneIdAsync(query.Token).WaitAsync(options.QueryTimeout, clock, round.Token);
                    }
                    finally { query.Cancel(); }
                    if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException(AppText.Get("网络位置未返回有效时区。", "The network location did not provide a valid time zone."));
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or JsonException or TimeoutException or OperationCanceledException)
                {
                    if (!IsCurrent(round)) return;
                    if (attempt < 2)
                    {
                        await Task.Delay(options.RetryDelay * (attempt + 1), clock, round.Token);
                        continue;
                    }
                    completed(new(SwitchStatus.Failed, AppText.Get("无法根据网络位置获取时区，请检查网络后重试。", "Unable to find the time zone for your network location. Check your connection and try again.")));
                    return;
                }
                if (!IsCurrent(round)) return;
                var result = await controller.SwitchAsync(id, false);
                if (IsCurrent(round) && result.Status != SwitchStatus.Unchanged) completed(result);
                return;
            }
        }
        catch (OperationCanceledException) when (round.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(pending, round)) pending = null;
            round.Dispose();
        }
    }

    private async Task WaitForQueryIntervalAsync(CancellationToken cancellationToken)
    {
        if (lastQuery is not { } previous) return;
        var remaining = options.MinimumQueryInterval - clock.GetElapsedTime(previous);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, clock, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        enabled = false;
        CancelPending();
    }
}
