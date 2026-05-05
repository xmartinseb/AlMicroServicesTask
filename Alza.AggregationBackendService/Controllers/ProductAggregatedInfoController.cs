using Alza.AggregationBackendService.Clients;
using Alza.AggregationBackendService.Models;
using Alza.HttpExtensions;
using Microsoft.AspNetCore.Mvc;

namespace Alza.AggregationBackendService.Controllers;

[ApiController]
[Route("[controller]")]
//[Authorize]
public sealed class ProductAggregatedInfoController(IProductClient productClient, IPricingClient pricingClient, IStockClient stockClient) : ControllerBase
{

    [HttpGet("{productId}")]
    public async Task<ProductAggregaredInfo> Get(Guid productId, CancellationToken cancellationToken)
    {
        var getProductTask = productClient.GetProductAsync(productId, cancellationToken);
        var getPriceTask = pricingClient.GetProductPriceAsync(productId, cancellationToken);
        var getAvailabilityTask = stockClient.GetProductAvailabilityAsync(productId, cancellationToken);

        //await Task.WhenAll(getProductTask, getPriceTask, getAvailabilityTask);

        var warnings = new List<string>();
        var product = await GetResultOrWarning(getProductTask, warnings, "Product service");
        var price = await GetResultOrWarning(getPriceTask, warnings, "Pricing service");
        var availability = await GetResultOrWarning(getAvailabilityTask, warnings, "Stock service");

        return new ProductAggregaredInfo(productId, product?.Name, product?.ImageUrl, price?.Price, availability?.Amount, warnings);
    }

    static async Task<T?> GetResultOrWarning<T>(Task<T> httpRequestTask, List<string> outputWarnings, string serviceName)
        where T : class
    {
        try
        {
            return await httpRequestTask;
        }
        catch (ExternalServiceHttpException ex)
        {
            outputWarnings.Add($"{serviceName} returned a http error state: {ex.StatusCode}");
        }
        catch (ExternalServiceTimeoutException ex)
        {
            outputWarnings.Add($"{serviceName} timeout");
        }
        catch (ExternalServiceException ex)
        {
            outputWarnings.Add($"{serviceName} failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            outputWarnings.Add($"{serviceName} failed with an unexpected error");
        }

        return null;
    }
}