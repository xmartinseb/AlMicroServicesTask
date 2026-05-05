using Alza.StockService.Models;

namespace Alza.StockService.Data;

public interface IStockDb
{
    Task<ProductAvailability> GetProductAvailabilityAsync(Guid productId, CancellationToken cancellationToken);
}
