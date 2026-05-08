using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Caches;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;

namespace Alza.AggregationBackendService.Clients;

public interface IProductClient
{
    Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class ResilientProductClient(HttpClient httpClient, IOptions<ProductClientOptions> clientOps, ILogger<ResilientProductClient> logger, ResilientProductClientCircuitBreaker circuitBreaker)
    : ResilientHttpClientBase(httpClient, logger, clientOps.Value.HttpRetryStrategy, circuitBreaker), IProductClient
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

public sealed class CachedProductClient(IProductClient productClient, InMemoryCacheWithSemaphores cache, IOptions<ProductClientOptions> options)
    : CachedClientBase<Product>(cache, options.Value.CachedDataTTL)

{
    protected override string CacheKeyPrefix => nameof(Product);

    protected override Task<Product> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => productClient.GetProductAsync(objectId, cancellationToken);

    protected override void AddLatencyMsToHistogram(int latencyMs) => serviceHttpLatencyHistogram.Record(latencyMs);

    private static readonly Histogram<int> serviceHttpLatencyHistogram = HttpLatencyMeter.CreateHistogram<int>("product_service_latency");
}

public sealed class ResilientProductClientCircuitBreaker : CircuitBreakerBase;