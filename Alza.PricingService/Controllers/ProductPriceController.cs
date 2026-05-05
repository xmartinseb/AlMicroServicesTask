using Alza.PricingService.Config;
using Alza.PricingService.Data;
using Alza.PricingService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Alza.PricingService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductPriceController(IPricingDb db, IMemoryCache cache, IOptions<CacheOptions> cacheConfig,
    ILogger<ProductPriceController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<ProductPrice> Get(Guid productId, CancellationToken cancellationToken)
        => (await cache.GetOrCreateAsync(productId.ToString(), async entry =>
        {
            var productPrice = await db.GetProductPriceAsync(productId, cancellationToken);
            var ttl = cacheConfig.Value.DataTTL;
            logger.LogInformation("Price of product {product} retrieved from db ({price})", productId, productPrice.Price);
            entry.AbsoluteExpirationRelativeToNow = ttl;
            return productPrice;
        }))!;
}