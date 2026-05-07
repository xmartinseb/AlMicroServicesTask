using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Caches;
using Microsoft.Extensions.Options;

namespace Alza.AggregationBackendService.Clients;

public interface IStockClient
{
    Task<ProductAvailability> GetProductAvailabilityAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class ResilientStockClient(HttpClient httpClient, IOptions<StockClientOptions> clientOps, ILogger<ResilientStockClient> logger) 
    : ResilientHttpClientBase(httpClient, logger, clientOps.Value.HttpRetryStrategy), IStockClient
{
    public Task<ProductAvailability> GetProductAvailabilityAsync(Guid productId, CancellationToken cancellationToken)
        => ExecuteRetryStrategy(client => client.UserFriendlyGetObjectAsync<ProductAvailability>($"/ProductAvailability/{productId}", cancellationToken), cancellationToken);
}

public sealed record ProductAvailability(Guid ProductId, int Amount);

public class StockClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan CachedDataTTL { get; set; }
    public HttpRetryStrategy HttpRetryStrategy { get; set; } = new();
}

public sealed class CachedStockClient(IStockClient stockClient, InMemoryCacheWithSemaphores cache, IOptions<StockClientOptions> options)
    : CachedClientBase<ProductAvailability>(cache, options.Value.CachedDataTTL)

{
    protected override string CacheKeyPrefix => nameof(ProductAvailability);

    protected override Task<ProductAvailability> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => stockClient.GetProductAvailabilityAsync(objectId, cancellationToken);
}