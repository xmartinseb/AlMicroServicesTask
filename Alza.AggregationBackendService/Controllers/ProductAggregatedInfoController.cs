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
    /// <summary>
    /// Retrieves aggregated product information, including details, pricing, and availability, for the specified
    /// product identifier.
    /// If any of the underlying services fail to provide data, the returned ProductAggregaredInfo
    /// will include warnings describing the issues encountered. Partial data may be returned if some services are
    /// unavailable.
    /// </summary>
    /// <param name="productId">The unique identifier of the product to retrieve aggregated information for.</param>
    /// <returns>Aggregated information about the product from all the services</returns>
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(ProductAggregaredInfo), 200)]
    public async Task<ActionResult<ProductAggregaredInfo>> Get(Guid productId, CancellationToken cancellationToken)
    {
        // TODO: neexistujici produkt by mel vratit 404
        var getProductTask = cachedProductClient.GetObjectAsync(productId, cancellationToken);
        var getPriceTask = cachedPricingClient.GetObjectAsync(productId, cancellationToken);
        var getAvailabilityTask = cachedStockClient.GetObjectAsync(productId, cancellationToken);

        var microservicesErrors = new List<SharedErrorModel>();
        var product = await GetResultOrWarning(getProductTask, microservicesErrors, "Product service", "PRODUCT_SERVICE_ERROR");
        var price = await GetResultOrWarning(getPriceTask, microservicesErrors, "Pricing service", "PRICING_SERVICE_ERROR");
        var availability = await GetResultOrWarning(getAvailabilityTask, microservicesErrors, "Stock service", "STOCK_SERVICE_ERROR");

        return new ProductAggregaredInfo(productId, product?.Name, product?.ImageUrl, price?.Price, availability?.Amount, microservicesErrors);
    }

    async Task<T?> GetResultOrWarning<T>(Task<T> loadingFromCachedClientTask, List<SharedErrorModel> outputWarnings, string serviceName, string serviceErrorCode)
        where T : class
    {
        try
        {
            return await loadingFromCachedClientTask;
        }
        catch (ExternalServiceHttpException ex)
        {
            logger.LogWarning(ex, "{Service} returned HTTP error {StatusCode}", serviceName, ex.StatusCode);
            outputWarnings.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} returned a http error state: {ex.StatusCode}"));
        }
        catch (ExternalServiceTimeoutException ex)
        {
            logger.LogWarning(ex, "{Service} timeout", serviceName);
            outputWarnings.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} timeout"));
        }
        catch (ExternalServiceException ex)
        {
            logger.LogError(ex, "{Service} failed", serviceName);
            outputWarnings.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} failed: {ex.Message}"));
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Loading from cached {Service} has been canceled", serviceName);
            outputWarnings.Add(new SharedErrorModel(serviceErrorCode, $"Loading from {serviceName} has been canceled"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure while loading data from cached {Service}", serviceName);
            outputWarnings.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} failed with an unexpected error"));
        }

        return null;
    }
}