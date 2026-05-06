using Alza.StockService.Config;
using Alza.StockService.Data;
using Alza.StockService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Alza.StockService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductAvailabilityController(IStockDb db, IMemoryCache cache, IOptions<CacheOptions> cacheConfig,
    ILogger<ProductAvailabilityController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(ProductAvailability), 200)]
    public async Task<ActionResult<ProductAvailability>> Get(Guid productId, CancellationToken cancellationToken)
        => (await cache.GetOrCreateAsync(productId.ToString(), async entry =>
        {
            var productAvail = await db.GetProductAvailabilityAsync(productId, cancellationToken);
            var ttl = cacheConfig.Value.DataTTL;
            logger.LogInformation("New cache entry: Product availability of the product {ProductId} = {Amount}; TTL={ttl}", productId, productAvail.Amount, ttl);
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.Size = 1;
            return productAvail;
        }))!;
}