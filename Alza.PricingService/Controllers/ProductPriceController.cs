using Alza.PricingService.Config;
using Alza.PricingService.Data;
using Alza.PricingService.Models;
using Caches;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;

namespace Alza.PricingService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductPriceController(IPricingDb db, InMemoryCacheWithSemaphores cache, IOptions<CacheOptions> cacheConfig,
    ILogger<ProductPriceController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(ProductPrice), 200)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<ProductPrice>> Get(Guid productId, CancellationToken cancellationToken)
    {
        // Note: this example requires some latency and occasional failures
        switch (Random.Shared.Next(5))
        {
            case 0:
                return StatusCode((int)HttpStatusCode.InternalServerError);
            case 1:
                return StatusCode((int)HttpStatusCode.NotFound);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(500, 800)), cancellationToken);
        return await cache.GetObjectAsync(productId.ToString(), async cancellationToken =>
            {
                var productPrice = await db.GetProductPriceAsync(productId, cancellationToken);
                logger.LogInformation("New cache entry: Price of the product {product} = {price}; TTL={ttl}",
                    productId, productPrice.Price, cacheConfig.Value.DataTTL);
                return productPrice;
            }, cacheConfig.Value.DataTTL, cancellationToken);
    }
}