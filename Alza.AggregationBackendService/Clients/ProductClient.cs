using Alza.AggregationBackendService.Clients.Cached;
using Alza.HttpExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Alza.AggregationBackendService.Clients;

public interface IProductClient
{
    Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class ProductClient(HttpClient client) : IProductClient
{
    public async Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await client.UserFriendlyGetObjectAsync<Product>($"/Product/{productId}", cancellationToken);
    }
}

public sealed record Product(Guid Id, string ImageUrl, string Name);
public class ProductClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan CachedDataTTL { get; set; }
}

public sealed class CachedProductClient(IProductClient productClient, IMemoryCache cache, IOptions<ProductClientOptions> options)
    : CachedClientBase<Product>(cache, options.Value.CachedDataTTL)

{
    protected override Task<Product> GetDataFromExternalServiceAsync(Guid objectId, CancellationToken cancellationToken)
        => productClient.GetProductAsync(objectId, cancellationToken);
}