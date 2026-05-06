using Alza.ProductService.Config;
using Alza.ProductService.Data;
using Alza.ProductService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Alza.ProductService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductController(IProductDb db, IMemoryCache cache, IOptions<CacheOptions> cacheConfig, 
    ILogger<ProductController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<Product> Get(Guid productId, CancellationToken cancellationToken)
        => (await cache.GetOrCreateAsync(productId.ToString(), async entry =>
        {
            var product = await db.GetProductAsync(productId, cancellationToken);
            var ttl = cacheConfig.Value.DataTTL;
            logger.LogInformation("New cache entry:  ProductId={productId}, name = {productName}, TTL={ttl}", product.Id, product.Name, ttl);
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.Size = 1;
            return product;
        }))!;
}