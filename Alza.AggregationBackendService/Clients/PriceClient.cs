using Alza.HttpExtensions;

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
    public TimeSpan CacheLoadedData { get; set; }
}