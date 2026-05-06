using Alza.PricingService.Config;
using Alza.PricingService.Data;
using Alza.PricingService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;

namespace Alza.PricingService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductPriceController(IPricingDb db, IMemoryCache cache, IOptions<CacheOptions> cacheConfig,
    ILogger<ProductPriceController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<ActionResult<ProductPrice>> Get(Guid productId, CancellationToken cancellationToken)
    {
        // Note: this example requires some latency and occasional failures
        if (Random.Shared.Next(100) < 50)
            return StatusCode((int)HttpStatusCode.InternalServerError);

        return (await cache.GetOrCreateAsync(productId.ToString(), async entry =>
            {
                // Note: this example requires some latency and occasional failures
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(500, 800)), cancellationToken);

                var productPrice = await db.GetProductPriceAsync(productId, cancellationToken);
                var ttl = cacheConfig.Value.DataTTL;
                logger.LogInformation("New cache entry: Price of the product {product} = {price}; TTL={ttl}", productId, productPrice.Price, ttl);
                entry.AbsoluteExpirationRelativeToNow = ttl;
                entry.Size = 1;
                return productPrice;
            }))!;
    }
}