using Alza.AggregationBackendService.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace Alza.IntegrTests;

public sealed class AggrServiceSuccessTest : IntegrationTestBase
{
    public AggrServiceSuccessTest() 
        : base(ServiceTestBehavior.Success, ServiceTestBehavior.Success, ServiceTestBehavior.Success)
    {
    }

    [Fact]
    public async Task Get_Returns200()
    {
        var productId = Guid.NewGuid();
        var aggrInfo = await client.GetProductAggregatedInfoAsync(productId);
        aggrInfo.Id.Should().Be(productId);
        aggrInfo.Name.Should().NotBeNull();
        aggrInfo.Price.Should().NotBeNull();
        aggrInfo.Availability.Should().NotBeNull();
    }
}

public sealed class AggrServicePartialSuccessTest : IntegrationTestBase
{
    public AggrServicePartialSuccessTest()
        : base(ServiceTestBehavior.Success, ServiceTestBehavior.Failure, ServiceTestBehavior.Failure)
    {
    }

    [Fact]
    public async Task Get_Returns200()
    {
        var productId = Guid.NewGuid();
        var aggrInfo = await client.GetProductAggregatedInfoAsync(productId);
        aggrInfo.Id.Should().Be(productId);
        aggrInfo.Name.Should().NotBeNull();
        aggrInfo.Price.Should().BeNull();
        aggrInfo.Availability.Should().BeNull();
    }
}

public sealed class AggrServicePartialSuccessWithTimeoutsTest : IntegrationTestBase
{
    public AggrServicePartialSuccessWithTimeoutsTest()
        : base(ServiceTestBehavior.Success, ServiceTestBehavior.Timeout, ServiceTestBehavior.Timeout)
    {
    }

    [Fact]
    public async Task Get_Returns200()
    {
        var productId = Guid.NewGuid();
        var aggrInfo = await client.GetProductAggregatedInfoAsync(productId);
        aggrInfo.Id.Should().Be(productId);
        aggrInfo.Name.Should().NotBeNull();
        aggrInfo.Price.Should().BeNull();
        aggrInfo.Availability.Should().BeNull();
    }
}

public sealed class AggrServiceServerErrorTest : IntegrationTestBase
{
    public AggrServiceServerErrorTest()
        : base(ServiceTestBehavior.Failure, ServiceTestBehavior.Success, ServiceTestBehavior.Success)
    {
    }

    [Fact]
    public async Task Get_Returns200()
    {
        var productId = Guid.NewGuid();
        var aggrInfo = await client.GetProductAggregatedInfoResponseAsync(productId);
        aggrInfo.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}

file static class RequestExtensions
{
    internal static async Task<HttpResponseMessage> GetProductAggregatedInfoResponseAsync(this HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/ProductAggregatedInfo/{productId}");
        response.Should().NotBeNull();
        return response;
    }

    internal static async Task<ProductAggregaredInfo> GetProductAggregatedInfoAsync(this HttpClient client, Guid productId)
    {
        var response = await client.GetProductAggregatedInfoResponseAsync(productId);
        var responseJson = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<ProductAggregaredInfo>(responseJson, DefaultJsonSerializerOptions);
        responseObj.Should().NotBeNull();
        return responseObj;
    }

    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}