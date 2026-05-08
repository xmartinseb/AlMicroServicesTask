using Alza.ProductService.Config;
using Alza.ProductService.Data;
using Alza.ProductService.Models;
using Caches;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Alza.ProductService.Controllers;

[ApiController]
[EnableRateLimiting("default")]
[Route("[controller]")]
public class ProductController(IProductDb db, InMemoryCacheWithSemaphores cache, IOptions<CacheOptions> cacheConfig,
    ILogger<ProductController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(Product), 200)]
    public async Task<ActionResult<Product>> Get(Guid productId, CancellationToken cancellationToken)
    {
        var (product, isCacheHit) = await cache.GetOrCreateObjectAsync(productId.ToString(), async cancellationToken =>
            {
                var product = await db.GetProductAsync(productId, cancellationToken);
                logger.LogInformation("New cache entry:  ProductId={productId}, name = {productName}, TTL={ttl}",
                    product.Id, product.Name, cacheConfig.Value.DataTTL);
                return product;
            }, cacheConfig.Value.DataTTL, cancellationToken);
        return product;
    }
}