using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Caches;

/// <summary>
/// Wraps an IMemoryCache and adds a synchronization to prevent multiple factory calls
/// (IMemoryCache helps with storage, but doesn't have synchronization over data retrieval -- it could lead to extra http requests in high concurrent scenarios)
/// This class MUST be registered as a singleton service, because it has to be shared between multiple HTTP requests.
/// </summary>
/// <param name="cache"></param>
public class InMemoryCacheWithSemaphores(IMemoryCache cache)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = [];

    // Note: It'd be great to introduce some removing of used semaphores in real production code

    public async Task<CacheLoadResult<TEntry>> GetOrCreateObjectAsync<TEntry>(string cacheKey, Func<CancellationToken, Task<TEntry>> factory, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(cacheKey, out TEntry? cached))
            return new(cached!, true);

        var semaphore = Locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            // double-check after lock
            if (cache.TryGetValue(cacheKey, out cached))
                return new(cached!, true);

            var downloadedValue = await factory(cancellationToken);
            cache.Set(cacheKey, downloadedValue, new MemoryCacheEntryOptions() { Size = 1, AbsoluteExpirationRelativeToNow = ttl });
            return new(downloadedValue, false);
        }
        finally
        {
            semaphore.Release();
        }
    }
}

public readonly record struct CacheLoadResult<TEntry>(TEntry Value, bool IsCacheHit);