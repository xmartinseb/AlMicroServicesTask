using Alza.PricingService.Data;
using Alza.PricingService.Models;
using Microsoft.AspNetCore.Mvc;

namespace Alza.PricingService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductPriceController(IPricingDb db) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<ProductPrice> Get(Guid productId, CancellationToken cancellationToken)
    {
        var price = await db.GetProductPriceAsync(productId, cancellationToken).ConfigureAwait(false);
        return price;
    }
}
