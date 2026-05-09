using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace Alza.IntegrationTests;

//public class ProductAggregatedInfoControllerTests
//{
//    [SetUp]
//    public void Setup()
//    {
//    }

//    [Test]
//    public void Test1()
//    {
//        Assert.Pass();
//    }
//}

public class ProductAggregatedInfoControllerTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public ProductAggregatedInfoControllerTests(
        WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Returns200()
    {
        var response =
            await client.GetAsync(
                "/ProductAggregatedInfo/11111111-1111-1111-1111-111111111111");

        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);
    }
}
