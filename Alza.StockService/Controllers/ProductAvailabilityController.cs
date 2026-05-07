using Alza.StockService.Config;
using Alza.StockService.Data;
using Alza.StockService.Models;
using Caches;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Alza.StockService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductAvailabilityController(IStockDb db, InMemoryCacheWithSemaphores cache, IOptions<CacheOptions> cacheConfig,
    ILogger<ProductAvailabilityController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(ProductAvailability), 200)]
    public async Task<ActionResult<ProductAvailability>> Get(Guid productId, CancellationToken cancellationToken)
        => await cache.GetObjectAsync(productId.ToString(), async cancellationToken =>
        {
            var productAvail = await db.GetProductAvailabilityAsync(productId, cancellationToken);
            logger.LogInformation("New cache entry: Product availability of the product {ProductId} = {Amount}; TTL = {ttl}", 
                productId, productAvail.Amount, cacheConfig.Value.DataTTL);
            return productAvail;
        }, cacheConfig.Value.DataTTL, cancellationToken);
}