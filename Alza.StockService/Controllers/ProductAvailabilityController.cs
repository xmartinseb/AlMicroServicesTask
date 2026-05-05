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
    public async Task<ProductAvailability> Get(Guid productId, CancellationToken cancellationToken)
        => (await cache.GetOrCreateAsync(productId.ToString(), async entry =>
        {
            var productAvail = await db.GetProductAvailabilityAsync(productId, cancellationToken);
            var ttl = cacheConfig.Value.DataTTL;
            logger.LogInformation("Product availability for {ProductId} retrieved from db: {}", productId, productAvail.Amount);
            entry.AbsoluteExpirationRelativeToNow = ttl;
            return productAvail;
        }))!;
}