using Alza.AggregationBackendService.Clients;
using Alza.HttpExtensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Alza.IntegrTests;

public enum ServiceTestBehavior
{
    Success,
    Failure,
    Timeout
}

public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly HttpClient client;

    protected IntegrationTestBase(ServiceTestBehavior productBehavior,ServiceTestBehavior pricingBehavior, ServiceTestBehavior stockBehavior)
    {
        var productMock = CreateProductMock(productBehavior);
        var pricingMock = CreatePricingMock(pricingBehavior);
        var stockMock = CreateStockMock(stockBehavior);

        var factory =
            new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IProductClient>();
                    services.RemoveAll<IPricingClient>();
                    services.RemoveAll<IStockClient>();

                    services.AddSingleton(productMock.Object);
                    services.AddSingleton(pricingMock.Object);
                    services.AddSingleton(stockMock.Object);
                });
            });

        client = factory.CreateClient();
    }

    private static Mock<IProductClient> CreateProductMock(ServiceTestBehavior behavior)
    {
        var mock = new Mock<IProductClient>();
        switch (behavior)
        {
            case ServiceTestBehavior.Success:
                mock.Setup(x => x.GetProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Product(Guid.Empty, "image", "RTX 5090"));
                break;

            case ServiceTestBehavior.Failure:
                mock.Setup(x => x.GetProductAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ExternalServiceException("boom"));
                break;

            case ServiceTestBehavior.Timeout:
                mock.Setup(x => x.GetProductAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ExternalServiceTimeoutException());
                break;
        }
        
        return mock;
    }

    private static Mock<IPricingClient> CreatePricingMock(ServiceTestBehavior behavior)
    {
        var mock = new Mock<IPricingClient>();
        switch (behavior)
        {
            case ServiceTestBehavior.Success:
                mock.Setup(x => x.GetProductPriceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ProductPrice(Guid.Empty, 0));
                break;

            case ServiceTestBehavior.Failure:
                mock.Setup(x => x.GetProductPriceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ExternalServiceException("TEST service exception"));
                break;

            case ServiceTestBehavior.Timeout:
                mock.Setup(x => x.GetProductPriceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ExternalServiceTimeoutException());
                break;
        }

        return mock;
    }

    private static Mock<IStockClient> CreateStockMock(ServiceTestBehavior behavior)
    {
        var mock = new Mock<IStockClient>();
        switch (behavior)
        {
            case ServiceTestBehavior.Success:
                mock.Setup(x => x.GetProductAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ProductAvailability(Guid.Empty, 0));
                break;

            case ServiceTestBehavior.Failure:
                mock.Setup(x => x.GetProductAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ExternalServiceException("TEST service exception"));
                break;

            case ServiceTestBehavior.Timeout:
                mock.Setup(x => x.GetProductAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ExternalServiceTimeoutException());
                break;
        }

        return mock;
    }
}