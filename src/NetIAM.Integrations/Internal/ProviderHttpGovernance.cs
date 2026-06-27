using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace NetIAM.Integrations.Internal;

internal static class ProviderHttpGovernance
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan MinimumProviderInterval = TimeSpan.FromMilliseconds(120);
    private static readonly ConcurrentDictionary<string, ProviderGate> ProviderGates = new(StringComparer.OrdinalIgnoreCase);

    public static Task<HttpResponseMessage> GetAsyncWithGovernance(
        this HttpClient httpClient,
        string providerCode,
        string url,
        CancellationToken cancellationToken = default)
    {
        return httpClient.SendAsyncWithGovernance(
            providerCode,
            () => new HttpRequestMessage(HttpMethod.Get, url),
            cancellationToken);
    }

    public static Task<HttpResponseMessage> PostJsonAsyncWithGovernance<TPayload>(
        this HttpClient httpClient,
        string providerCode,
        string url,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        return httpClient.SendAsyncWithGovernance(
            providerCode,
            () => new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            },
            cancellationToken);
    }

    public static async Task<HttpResponseMessage> SendAsyncWithGovernance(
        this HttpClient httpClient,
        string providerCode,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await WaitForProviderSlotAsync(providerCode, cancellationToken);
            using var request = requestFactory();
            try
            {
                var response = await httpClient.SendAsync(request, cancellationToken);
                if (IsRetryableStatusCode(response.StatusCode) && attempt < MaxAttempts)
                {
                    var retryDelay = ResolveRetryDelay(response, attempt);
                    response.Dispose();
                    await Task.Delay(retryDelay, cancellationToken);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts)
            {
                lastException = ex;
                await Task.Delay(CalculateBackoff(attempt), cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
            {
                lastException = ex;
                await Task.Delay(CalculateBackoff(attempt), cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException($"Provider call failed after {MaxAttempts} attempts.");
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests
               || statusCode == HttpStatusCode.BadGateway
               || statusCode == HttpStatusCode.ServiceUnavailable
               || statusCode == HttpStatusCode.GatewayTimeout
               || (int)statusCode >= 500;
    }

    private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta > MaxDelay ? MaxDelay : delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var computed = date - DateTimeOffset.UtcNow;
            if (computed > TimeSpan.Zero)
            {
                return computed > MaxDelay ? MaxDelay : computed;
            }
        }

        return CalculateBackoff(attempt);
    }

    private static TimeSpan CalculateBackoff(int attempt)
    {
        var exponential = TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, Math.Max(0, attempt - 1)));
        if (exponential > MaxDelay)
        {
            exponential = MaxDelay;
        }

        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(20, 180));
        var total = exponential + jitter;
        return total > MaxDelay ? MaxDelay : total;
    }

    private static async Task WaitForProviderSlotAsync(string providerCode, CancellationToken cancellationToken)
    {
        var gate = ProviderGates.GetOrAdd(providerCode, _ => new ProviderGate());
        await gate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (gate.NextAllowedAt > now)
            {
                await Task.Delay(gate.NextAllowedAt - now, cancellationToken);
            }

            gate.NextAllowedAt = DateTimeOffset.UtcNow + MinimumProviderInterval;
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    private sealed class ProviderGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public DateTimeOffset NextAllowedAt { get; set; }
    }
}
