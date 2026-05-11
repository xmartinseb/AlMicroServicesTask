using Alza.AggregationBackendService.Clients;
using Alza.AggregationBackendService.Models;
using Alza.HttpExtensions;

namespace Alza.AggregationBackendService;

public interface IProductAggregatedInfoService
{
    Task<ProductAggregaredInfo> GetAggregatedProductInfoAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class ProductAggregatedInfoService(ILogger<ProductAggregatedInfoService> logger,
    CachedProductClient cachedProductClient, CachedPricingClient cachedPricingClient, CachedStockClient cachedStockClient)
    : IProductAggregatedInfoService
{
    public async Task<ProductAggregaredInfo> GetAggregatedProductInfoAsync(Guid productId, CancellationToken cancellationToken)
    {
        var getProductTask = cachedProductClient.GetObjectAsync(productId, cancellationToken);
        var getPriceTask = cachedPricingClient.GetObjectAsync(productId, cancellationToken);
        var getAvailabilityTask = cachedStockClient.GetObjectAsync(productId, cancellationToken);

        var microservicesErrors = new List<SharedErrorModel>();
        // Note: product detail is CRITICAL service - if it fails, we can't return any meaningful data to the caller, so we throw an exception and fail the whole HTTP request with an error status code.
        Product product = await GetResultOrThrowAsync(getProductTask, "Product service");

        // Note: price and availability details are NON-CRITICAL - if any of those fail, we still return data from other services with error details in the response
        var price = await TryGetOptionalServiceDataAsync(getPriceTask, microservicesErrors, "Pricing service", "PRICING_SERVICE_ERROR");
        var availability = await TryGetOptionalServiceDataAsync(getAvailabilityTask, microservicesErrors, "Stock service", "STOCK_SERVICE_ERROR");

        return new ProductAggregaredInfo(productId, product?.Name, product?.ImageUrl, price?.Price, availability?.Amount, microservicesErrors);
    }

    /// <summary>
    /// Calls critical microservices (if they fail, we can't return any meaningful data to the caller and the whole HTTP request should fail)
    /// Every exception is logged and re-thrown.
    /// </summary>
    async Task<T> GetResultOrThrowAsync<T>(Task<T> loadingFromCachedClientTask, string serviceName)
    {
        try
        {
            return await loadingFromCachedClientTask;
        }
        catch (CircuitBreakerBlocksException ex)
        {
            logger.LogWarning(ex, "{Service} is blocked by circuit breaker due to recent failures", serviceName);
            throw;
        }
        catch (ExternalServiceHttpException ex)
        {
            logger.LogWarning(ex, "{Service} returned HTTP error {StatusCode}", serviceName, ex.StatusCode);
            throw;
        }
        catch (ExternalServiceTimeoutException ex)
        {
            logger.LogWarning(ex, "{Service} timeout", serviceName);
            throw;
        }
        catch (ExternalServiceException ex)
        {
            logger.LogError(ex, "{Service} failed", serviceName);
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure while loading data from cached {Service}", serviceName);
            throw;
        }
    }

    /// <summary>
    /// Calls non-critical microservices (if they fail, we still return partial data to the caller with error list within the response)
    /// </summary>
    async Task<T?> TryGetOptionalServiceDataAsync<T>(Task<T> loadingFromCachedClientTask, List<SharedErrorModel> outputErrors, string serviceName, string serviceErrorCode)
      where T : class
    {
        try
        {
            return await GetResultOrThrowAsync(loadingFromCachedClientTask, serviceName);
        }
        catch (CircuitBreakerBlocksException)
        {
            outputErrors.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} is unavailable. Try later!"));
        }
        catch (ExternalServiceHttpException ex)
        {
            outputErrors.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} returned a http error state: {ex.StatusCode}"));
        }
        catch (ExternalServiceTimeoutException)
        {
            outputErrors.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} timeout"));
        }
        catch (ExternalServiceException ex)
        {
            outputErrors.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} failed: {ex.Message}"));
        }
        catch (OperationCanceledException)
        {
            outputErrors.Add(new SharedErrorModel(serviceErrorCode, $"Loading from {serviceName} has been canceled"));
        }
        catch (Exception)
        {
            outputErrors.Add(new SharedErrorModel(serviceErrorCode, $"{serviceName} failed with an unexpected error"));
        }
        return null;
    }
}