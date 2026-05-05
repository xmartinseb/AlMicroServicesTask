using Alza.PricingService.Models;

namespace Alza.PricingService.Data;

public interface IPricingDb
{
    Task<ProductPrice> GetProductPriceAsync(Guid productId, CancellationToken cancellationToken);
}
