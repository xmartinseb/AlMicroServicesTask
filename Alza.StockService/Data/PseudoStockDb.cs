using Alza.StockService.Models;

namespace Alza.StockService.Data;

public sealed class PseudoStockDb : IStockDb
{
    public async Task<ProductAvailability> GetProductAvailabilityAsync(Guid productId, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(200 * Random.Shared.NextDouble()), cancellationToken).ConfigureAwait(false);
        return new ProductAvailability(productId, Random.Shared.Next(5));
    }
}
