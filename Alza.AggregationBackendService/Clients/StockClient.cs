using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Microsoft.Extensions.Caching.Memory;

namespace Alza.AggregationBackendService.Clients;

public interface IStockClient
{
    Task<ProductAvailability> GetProductAvailabilityAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class StockClient(HttpClient client) : IStockClient
{
    public async Task<ProductAvailability> GetProductAvailabilityAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await client.UserFriendlyGetObjectAsync<ProductAvailability>($"/ProductAvailability/{productId}", cancellationToken);
    }
}

public sealed record ProductAvailability(Guid ProductId, int Amount);

public class StockClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan CachedDataTTL { get; set; }
}

public sealed class CachedStockClient(IStockClient stockClient, IMemoryCache cache, StockClientOptions options)
    : CachedClientBase<ProductAvailability>(cache, options.CachedDataTTL)

{
    protected override Task<ProductAvailability> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => stockClient.GetProductAvailabilityAsync(objectId, cancellationToken);
}