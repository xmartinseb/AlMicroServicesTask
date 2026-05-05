using Alza.PricingService.Models;

namespace Alza.PricingService.Data;

public sealed class PseudoPricingDb : IPricingDb
{
    public async Task<ProductPrice> GetProductPriceAsync(Guid productId, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(200 * Random.Shared.NextDouble()), cancellationToken);
        return new ProductPrice(productId, Random.Shared.Next(100, 1000));
    }
}
