using System.Net;
using System.Text;
using System.Text.Json;
using Domain.Interfaces;
using Infrastructure.CountyLookup;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for CountyLookupService — backed by the Financial Planner API.
/// Uses a mocked HttpMessageHandler to simulate API responses.
/// </summary>
public class CountyLookupServiceTests
{
    private static CountyLookupService CreateService(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FinancialPlanner:BaseUrl"] = "http://test-api/api/v1"
            })
            .Build();

        return new CountyLookupService(httpClient, cache, config, NullLogger<CountyLookupService>.Instance);
    }

    private static HttpResponseMessage CreateApiResponse(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private static object CreateColoradoResponse() => new
    {
        webServiceTransactionId = "test-123",
        webServiceStatus = "SUCCESS",
        zipcode = "80113",
        state = (object?)null,
        countycodeList = new[]
        {
            new { zipcode = "80113", state = "Colorado", stateCode = "CO", city = "CHERRY HILLS", latitude = 39.6474, longitude = -104.973, countyCode = "8005", countyName = "ARAPAHOE" },
            new { zipcode = "80113", state = "Colorado", stateCode = "CO", city = "ENGLEWOOD", latitude = 39.6474, longitude = -104.973, countyCode = "8005", countyName = "ARAPAHOE" }
        }
    };

    private static object CreateEmptyResponse() => new
    {
        webServiceTransactionId = "test-456",
        webServiceStatus = "SUCCESS",
        zipcode = "00000",
        state = (object?)null,
        countycodeList = Array.Empty<object>()
    };

    // ═══════ GetCountyCode ═══════

    [Fact]
    public async Task GetCountyCode_ValidZip_ReturnsCountyCode()
    {
        var service = CreateService(CreateApiResponse(CreateColoradoResponse()));
        var code = await service.GetCountyCode("80113");
        Assert.Equal("8005", code);
    }

    [Fact]
    public async Task GetCountyCode_EmptyString_ReturnsNull()
    {
        var service = CreateService(CreateApiResponse(Array.Empty<object>()));
        var code = await service.GetCountyCode("");
        Assert.Null(code);
    }

    [Fact]
    public async Task GetCountyCode_NullInput_ReturnsNull()
    {
        var service = CreateService(CreateApiResponse(Array.Empty<object>()));
        var code = await service.GetCountyCode(null!);
        Assert.Null(code);
    }

    // ═══════ GetCountyName ═══════

    [Fact]
    public async Task GetCountyName_ValidZip_ReturnsCountyName()
    {
        var service = CreateService(CreateApiResponse(CreateColoradoResponse()));
        var name = await service.GetCountyName("80113");
        Assert.Equal("ARAPAHOE", name);
    }

    [Fact]
    public async Task GetCountyName_EmptyZip_ReturnsNull()
    {
        var service = CreateService(CreateApiResponse(Array.Empty<object>()));
        var name = await service.GetCountyName("");
        Assert.Null(name);
    }

    // ═══════ GetStateCode ═══════

    [Fact]
    public async Task GetStateCode_ValidZip_ReturnsStateCode()
    {
        var service = CreateService(CreateApiResponse(CreateColoradoResponse()));
        var state = await service.GetStateCode("80113");
        Assert.Equal("CO", state);
    }

    [Fact]
    public async Task GetStateCode_NullInput_ReturnsNull()
    {
        var service = CreateService(CreateApiResponse(Array.Empty<object>()));
        var state = await service.GetStateCode(null!);
        Assert.Null(state);
    }

    // ═══════ GetCountyCodeList ═══════

    [Fact]
    public async Task GetCountyCodeList_ValidZip_ReturnsMultipleEntries()
    {
        var service = CreateService(CreateApiResponse(CreateColoradoResponse()));
        var results = await service.GetCountyCodeList("80113");

        Assert.Equal(2, results.Count);
        Assert.Equal("CHERRY HILLS", results[0].City);
        Assert.Equal("ARAPAHOE", results[0].CountyName);
        Assert.Equal("8005", results[0].CountyCode);
        Assert.Equal("CO", results[0].State);
    }

    [Fact]
    public async Task GetCountyCodeList_EmptyZip_ReturnsEmptyList()
    {
        var service = CreateService(CreateApiResponse(Array.Empty<object>()));
        var results = await service.GetCountyCodeList("");
        Assert.Empty(results);
    }

    // ═══════ Caching ═══════

    [Fact]
    public async Task GetCountyCode_SecondCall_UsesCachedResult()
    {
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return CreateApiResponse(CreateColoradoResponse());
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FinancialPlanner:BaseUrl"] = "http://test-api/api/v1"
            })
            .Build();

        var service = new CountyLookupService(httpClient, cache, config, NullLogger<CountyLookupService>.Instance);

        await service.GetCountyCode("80113");
        await service.GetCountyCode("80113");

        Assert.Equal(1, callCount); // Only one HTTP call, second was cached
    }

    // ═══════ API Error Handling ═══════

    [Fact]
    public async Task GetCountyCode_ApiError_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var service = CreateService(response);
        var code = await service.GetCountyCode("80113");
        Assert.Null(code);
    }
}
