using Alza.ProductService.Models;

namespace Alza.ProductService.Data;

public interface IProductDb
{
    Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}
