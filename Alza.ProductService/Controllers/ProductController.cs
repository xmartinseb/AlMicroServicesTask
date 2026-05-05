using Alza.ProductService.Data;
using Alza.ProductService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Alza.ProductService.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductController(IProductDb db, IMemoryCache cache) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<Product> Get(Guid productId, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(productId.ToString(), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return await db.GetProductAsync(productId, cancellationToken);
        }) ?? throw new Exception("Loaded product is null");
}