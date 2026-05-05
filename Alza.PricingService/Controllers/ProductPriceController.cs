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
        // Note: this service requires some latency and occasional failures to simulate real-world conditions and test resilience of the system.
        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(500, 800)), cancellationToken);
        if (Random.Shared.Next(100) < 10)
            throw new Exception("Random failure in pricing service");
        
        var price = await db.GetProductPriceAsync(productId, cancellationToken);
        return price;
    }
}
