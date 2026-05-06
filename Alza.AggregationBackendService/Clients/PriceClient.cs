using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Alza.AggregationBackendService.Clients;

public interface IPricingClient
{
    Task<ProductPrice> GetProductPriceAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class PricingClient(HttpClient client) : IPricingClient
{
    public async Task<ProductPrice> GetProductPriceAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await client.UserFriendlyGetObjectAsync<ProductPrice>($"/ProductPrice/{productId}", cancellationToken);
    }
}

public sealed record ProductPrice(Guid ProductId, double Price);

public class PricingClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan CachedDataTTL { get; set; }
}

public sealed class CachedPricingClient(IPricingClient pricingClient, IMemoryCache cache, IOptions<PricingClientOptions> options)
    : CachedClientBase<ProductPrice>(cache, options.Value.CachedDataTTL)

{
    protected override Task<ProductPrice> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => pricingClient.GetProductPriceAsync(objectId, cancellationToken);
}