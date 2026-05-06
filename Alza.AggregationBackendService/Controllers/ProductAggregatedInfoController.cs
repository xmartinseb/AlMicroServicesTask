using Alza.AggregationBackendService.Clients;
using Alza.AggregationBackendService.Models;
using Alza.HttpExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Alza.AggregationBackendService.Controllers;

[ApiController]
[Route("[controller]")]
//[Authorize]
[EnableRateLimiting("default")]
public sealed class ProductAggregatedInfoController(
    CachedProductClient cachedProductClient, CachedPricingClient cachedPricingClient, CachedStockClient cachedStockClient,
    ILogger<ProductAggregatedInfoController> logger) : ControllerBase
{
    [HttpGet("{productId}")]
    public async Task<ProductAggregaredInfo> Get(Guid productId, CancellationToken cancellationToken)
    {
        var getProductTask = cachedProductClient.GetObjectAsync(productId, cancellationToken);
        var getPriceTask = cachedPricingClient.GetObjectAsync(productId, cancellationToken);
        var getAvailabilityTask = cachedStockClient.GetObjectAsync(productId, cancellationToken);

        var warnings = new List<string>();
        var product = await GetResultOrWarning(getProductTask, warnings, "Product service");
        var price = await GetResultOrWarning(getPriceTask, warnings, "Pricing service");
        var availability = await GetResultOrWarning(getAvailabilityTask, warnings, "Stock service");

        return new ProductAggregaredInfo(productId, product?.Name, product?.ImageUrl, price?.Price, availability?.Amount, warnings);
    }

    async Task<T?> GetResultOrWarning<T>(Task<T> loadingFromCachedClientTask, List<string> outputWarnings, string serviceName)
        where T : class
    {
        try
        {
            return await loadingFromCachedClientTask;
        }
        catch (ExternalServiceHttpException ex)
        {
            logger.LogWarning(ex, "{Service} returned HTTP error {StatusCode}", serviceName, ex.StatusCode);
            outputWarnings.Add($"{serviceName} returned a http error state: {ex.StatusCode}");
        }
        catch (ExternalServiceTimeoutException ex)
        {
            logger.LogWarning(ex, "{Service} timeout", serviceName);
            outputWarnings.Add($"{serviceName} timeout");
        }
        catch (ExternalServiceException ex)
        {
            logger.LogError(ex, "{Service} failed", serviceName);
            outputWarnings.Add($"{serviceName} failed: {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Loading from cached {Service} has been canceled", serviceName);
            outputWarnings.Add($"Loading from {serviceName} has been canceled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure while loading data from cached {Service}", serviceName);
            outputWarnings.Add($"{serviceName} failed with an unexpected error");
        }

        return null;
    }
}