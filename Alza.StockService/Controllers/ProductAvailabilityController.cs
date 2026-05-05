using Alza.StockService.Data;
using Alza.StockService.Models;
using Microsoft.AspNetCore.Mvc;

namespace Alza.StockService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductAvailabilityController(IStockDb db) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<ProductAvailability> Get(Guid productId, CancellationToken cancellationToken)
    {
        var productAvail = await db.GetProductAvailabilityAsync(productId, cancellationToken);
        return productAvail;
    }
}
