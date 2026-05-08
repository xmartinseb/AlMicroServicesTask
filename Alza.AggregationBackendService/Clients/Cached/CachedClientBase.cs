using Caches;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Alza.AggregationBackendService.Clients.Cached;

/// <summary>
/// Base class for wrapper clients that add a caching layer on top of the HTTP communication.
/// This mechanism improves the latency.
/// </summary>
/// <typeparam name="TEntry">Entity type that is being loaded from the cache or from the microservice</typeparam>
public abstract class CachedClientBase<TEntry>(InMemoryCacheWithSemaphores cache, TimeSpan cacheEntriesTTL)
    where TEntry : class
{
    protected abstract string CacheKeyPrefix { get; }
    protected abstract Task<TEntry> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken);

    protected abstract void AddLatencyMsToHistogram(int latencyMs);

    public async Task<TEntry> GetObjectAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var (value, isCacheHit) = await cache.GetOrCreateObjectAsync($"{CacheKeyPrefix}:{objectId}",
                cancellation =>
                {
                    var latencyStopwatch = Stopwatch.StartNew();
                    var downloadedEntity = GetDataFromExternalServiceAsync(objectId, cancellation);
                    var latency = latencyStopwatch.ElapsedMilliseconds;
                    AddLatencyMsToHistogram((int)latency);
                    
                    return downloadedEntity;
                },
                cacheEntriesTTL, cancellationToken);
        if (isCacheHit)
            CacheHits.Add(1);
        else
            CacheMisses.Add(1);
        return value;
    }
    
    // Metrics
    static readonly Meter CacheMeter = new("CacheHitMiss");
    protected static readonly Meter HttpLatencyMeter = new("DownstreamLatency");

    static readonly Counter<int> CacheHits = CacheMeter.CreateCounter<int>("cache_hits_total");
    static readonly Counter<int> CacheMisses = CacheMeter.CreateCounter<int>("cache_misses_total");
}