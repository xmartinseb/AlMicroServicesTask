using Microsoft.Extensions.Caching.Memory;

namespace Alza.AggregationBackendService.Clients.Cached;

public abstract class CachedClientBase<TEntry>(IMemoryCache cache, TimeSpan cacheEntriesTTL)
    where TEntry : class
{
    public readonly string CacheKeyPrefix = typeof(TEntry).FullName!;
    protected abstract Task<TEntry> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken);

    public async Task<TEntry> GetObjectAsync(Guid objectId, CancellationToken cancellationToken)
        => (await cache.GetOrCreateAsync($"{CacheKeyPrefix}_{objectId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = cacheEntriesTTL;
            return await GetDataFromExternalServiceAsync(objectId, cancellationToken);
        }))!;
}