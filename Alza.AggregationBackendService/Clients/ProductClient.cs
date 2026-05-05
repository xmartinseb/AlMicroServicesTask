using Alza.HttpExtensions;

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
    public TimeSpan CacheLoadedData { get; set; }
}