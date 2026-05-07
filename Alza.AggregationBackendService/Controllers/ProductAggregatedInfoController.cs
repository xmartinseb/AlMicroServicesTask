using Alza.AggregationBackendService.Models;
using Alza.HttpExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;

namespace Alza.AggregationBackendService.Controllers;

[ApiController]
[Route("[controller]")]
//[Authorize]
[EnableRateLimiting("default")]
public sealed class ProductAggregatedInfoController(IProductAggregatedInfoService productAggregationService, ILogger<ProductAggregatedInfoController> logger) 
    : ControllerBase
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
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    [ProducesResponseType((int)HttpStatusCode.RequestTimeout)]
    public async Task<ActionResult<ProductAggregaredInfo>> Get(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            return await productAggregationService.GetAggregatedProductInfoAsync(productId, cancellationToken);
        }
        catch (ExternalServiceTimeoutException)
        {
            return StatusCode((int)HttpStatusCode.RequestTimeout);
        }
        catch (ExternalServiceException)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError);
        }
    }
}