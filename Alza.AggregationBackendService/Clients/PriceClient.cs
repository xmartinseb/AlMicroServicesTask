using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Caches;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;

namespace Alza.AggregationBackendService.Clients;

/// <summary>
/// Defines HTTP operations for the pricing microservice
/// </summary>
public interface IPricingClient
{
    Task<ProductPrice> GetProductPriceAsync(Guid productId, CancellationToken cancellationToken);
}

/// <summary>
/// Defines HTTP operations for the pricing microservice.
/// It also uses a retry strategy to handle unavailability of the microservice or transient errors. 
/// The strategy is defined in the configuration and is applied to all HTTP calls to the microservice.
/// </summary>
public sealed class ResilientPricingClient(HttpClient httpClient, IOptions<PricingClientOptions> clientOps, ILogger<ResilientPricingClient> logger)
    : ResilientHttpClientBase(httpClient, logger, clientOps.Value.HttpRetryStrategy), IPricingClient
{
    public Task<ProductPrice> GetProductPriceAsync(Guid productId, CancellationToken cancellationToken)
        => ExecuteRetryStrategy(client => client.UserFriendlyGetObjectAsync<ProductPrice>($"/ProductPrice/{productId}", cancellationToken), cancellationToken);
}

/// <summary>
/// Model representing the price of a product, as returned by the pricing microservice
/// </summary>
public sealed record ProductPrice(Guid ProductId, double Price);

/// <summary>
/// Configuration for HTTP communication with the pricing microservice
/// </summary>
public class PricingClientOptions
{
    /// <summary>
    /// Microservice url
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Aggr. service keeps the data from its microservices cached for a certain time.
    /// </summary>
    public TimeSpan CachedDataTTL { get; set; }

    /// <summary>
    /// Describes the retry strategy that is used when the microservice is unavailable or returns an error. 
    /// The strategy is applied to all HTTP calls to the microservice.
    /// </summary>
    public HttpRetryStrategy HttpRetryStrategy { get; set; } = new();
}

/// <summary>
/// Adds a in-memory cache layer on top of the pricing client.
/// It uses singleton cache.
/// </summary>
public sealed class CachedPricingClient(IPricingClient pricingClient, InMemoryCacheWithSemaphores cache, IOptions<PricingClientOptions> options)
    : CachedClientBase<ProductPrice>(cache, options.Value.CachedDataTTL)

{
    protected override string CacheKeyPrefix => nameof(ProductPrice);

    protected override Task<ProductPrice> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => pricingClient.GetProductPriceAsync(objectId, cancellationToken);

    protected override void AddLatencyMsToHistogram(int latencyMs) => serviceHttpLatencyHistogram.Record(latencyMs);

    private static readonly Histogram<int> serviceHttpLatencyHistogram = HttpLatencyMeter.CreateHistogram<int>("pricing_service_latency");
}