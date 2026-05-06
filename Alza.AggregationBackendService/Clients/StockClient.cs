using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Alza.AggregationBackendService.Clients;

public interface IStockClient
{
    Task<ProductAvailability> GetProductAvailabilityAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class StockClient(HttpClient httpClient, IOptions<StockClientOptions> clientOps) 
    : ResilientHttpClientBase(httpClient, clientOps.Value.HttpRetryStrategy), IStockClient
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

public sealed class CachedStockClient(IStockClient stockClient, IMemoryCache cache, IOptions<StockClientOptions> options)
    : CachedClientBase<ProductAvailability>(cache, options.Value.CachedDataTTL)

{
    protected override Task<ProductAvailability> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => stockClient.GetProductAvailabilityAsync(objectId, cancellationToken);
}