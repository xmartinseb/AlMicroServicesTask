using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Alza.AggregationBackendService.Clients;

public interface IProductClient
{
    Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class ResilientProductClient(HttpClient httpClient, IOptions<ProductClientOptions> clientOps, ILogger<ResilientProductClient> logger)
    : ResilientHttpClientBase(httpClient, logger, clientOps.Value.HttpRetryStrategy), IProductClient
{
    public Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken)
        => ExecuteRetryStrategy(client => client.UserFriendlyGetObjectAsync<Product>($"/Product/{productId}", cancellationToken), cancellationToken);
}

public sealed record Product(Guid Id, string ImageUrl, string Name);
public class ProductClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan CachedDataTTL { get; set; }
    public HttpRetryStrategy HttpRetryStrategy { get; set; } = new();
}

public sealed class CachedProductClient(IProductClient productClient, IMemoryCache cache, IOptions<ProductClientOptions> options)
    : CachedClientBase<Product>(cache, options.Value.CachedDataTTL)

{
    protected override Task<Product> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => productClient.GetProductAsync(objectId, cancellationToken);
}