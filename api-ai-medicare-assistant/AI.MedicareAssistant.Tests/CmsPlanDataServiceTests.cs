using System.Net;
using System.Text;
using System.Text.Json;
using Domain.Models;
using Infrastructure.Medicare;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for CmsPlanDataService — CMS SOCRATA API queries with sample response data.
/// Uses a mock HttpMessageHandler to simulate CMS API responses.
/// </summary>
public class CmsPlanDataServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<CmsPlanDataService>> _loggerMock;
    private readonly IConfiguration _config;

    public CmsPlanDataServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<CmsPlanDataService>>();
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CMS:PlanLandscapeUrl"] = "https://data.cms.gov/resource/yjmm-q4z2.json",
                ["CMS:FormularyUrl"] = "https://data.cms.gov/resource/v6p3-p4zt.json"
            })
            .Build();
    }

    // ═══════ GetPlansForAreaAsync ═══════

    [Fact]
    public async Task GetPlansForAreaAsync_ReturnsParsedPlans()
    {
        // Arrange
        var responseData = new[]
        {
            new
            {
                Contract_Id = "H1234",
                Plan_Id = "001",
                Plan_Name = "Humana Gold Plus HMO-POS",
                Organization_Name = "Humana Insurance Company",
                Plan_Type = "MA-PD",
                Monthly_Premium = "0.00",
                Annual_Deductible = "0.00",
                Overall_Star_Rating = "4.5",
                State = "NY",
                County = "NEW YORK"
            },
            new
            {
                Contract_Id = "S5678",
                Plan_Id = "002",
                Plan_Name = "Aetna CVS Health Medicare Value",
                Organization_Name = "Aetna Inc",
                Plan_Type = "MA-PD",
                Monthly_Premium = "25.00",
                Annual_Deductible = "200.00",
                Overall_Star_Rating = "4.0",
                State = "NY",
                County = "NEW YORK"
            }
        };

        var sut = CreateService(HttpStatusCode.OK, responseData);

        // Act
        var result = await sut.GetPlansForAreaAsync("NY", "New York");

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal("H1234", result[0].ContractId);
        Assert.Equal("001", result[0].PlanId);
        Assert.Equal("Humana Gold Plus HMO-POS", result[0].PlanName);
        Assert.Equal("Humana Insurance Company", result[0].OrganizationName);
        Assert.Equal(0m, result[0].MonthlyPremium);
        Assert.Equal(4.5m, result[0].StarRating);

        Assert.Equal("S5678", result[1].ContractId);
        Assert.Equal(25m, result[1].MonthlyPremium);
        Assert.Equal(200m, result[1].AnnualDeductible);
    }

    [Fact]
    public async Task GetPlansForAreaAsync_EmptyState_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.OK, Array.Empty<object>());
        var result = await sut.GetPlansForAreaAsync("", "New York");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlansForAreaAsync_EmptyCounty_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.OK, Array.Empty<object>());
        var result = await sut.GetPlansForAreaAsync("NY", "");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlansForAreaAsync_NullInput_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.OK, Array.Empty<object>());
        var result = await sut.GetPlansForAreaAsync(null!, null!);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlansForAreaAsync_ApiError_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.InternalServerError, null);

        var result = await sut.GetPlansForAreaAsync("NY", "New York");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlansForAreaAsync_NoResults_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.OK, Array.Empty<object>());

        var result = await sut.GetPlansForAreaAsync("ZZ", "Nonexistent County");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPlansForAreaAsync_CachesResults()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, new[]
        {
            new { Contract_Id = "H1234", Plan_Id = "001", Plan_Name = "Test Plan",
                  Organization_Name = "Test", Plan_Type = "MA-PD",
                  Monthly_Premium = "10.00", Annual_Deductible = "100.00",
                  Overall_Star_Rating = "3.5", State = "NY", County = "KINGS" }
        });

        var client = new HttpClient(handler);
        var sut = new CmsPlanDataService(client, _cache, _config, _loggerMock.Object);

        // First call
        var result1 = await sut.GetPlansForAreaAsync("NY", "Kings");
        // Second call (should be cached)
        var result2 = await sut.GetPlansForAreaAsync("NY", "Kings");

        Assert.Single(result1);
        Assert.Single(result2);
        Assert.Equal(1, handler.CallCount); // HTTP called only once
    }

    [Fact]
    public async Task GetPlansForAreaAsync_HandlesMissingFields()
    {
        // CMS data with null/missing fields
        var responseData = new[]
        {
            new
            {
                Contract_Id = (string?)null,
                Plan_Id = (string?)null,
                Plan_Name = (string?)null,
                Organization_Name = (string?)null,
                Plan_Type = (string?)null,
                Monthly_Premium = (string?)null,
                Annual_Deductible = (string?)null,
                Overall_Star_Rating = (string?)null,
                State = (string?)null,
                County = (string?)null
            }
        };

        var sut = CreateService(HttpStatusCode.OK, responseData);

        var result = await sut.GetPlansForAreaAsync("NY", "New York");

        Assert.Single(result);
        Assert.Equal("", result[0].ContractId);
        Assert.Equal("", result[0].PlanName);
        Assert.Equal(0m, result[0].MonthlyPremium);
        Assert.Equal(0m, result[0].StarRating);
    }

    // ═══════ GetFormularyEntriesAsync ═══════

    [Fact]
    public async Task GetFormularyEntriesAsync_ReturnsParsedEntries()
    {
        var responseData = new[]
        {
            new
            {
                Contract_Id = "H1234",
                Rxcui = "197361",
                Drug_Name = "LISINOPRIL 10MG TABLET",
                Tier_Level = "1",
                Prior_Auth = "N",
                Quantity_Limit = "N",
                Step_Therapy = "N"
            },
            new
            {
                Contract_Id = "H1234",
                Rxcui = "312961",
                Drug_Name = "METFORMIN HCL 500MG TABLET",
                Tier_Level = "1",
                Prior_Auth = "N",
                Quantity_Limit = "Y",
                Step_Therapy = "N"
            },
            new
            {
                Contract_Id = "H1234",
                Rxcui = "198211",
                Drug_Name = "ATORVASTATIN CALCIUM 20MG TABLET",
                Tier_Level = "2",
                Prior_Auth = "N",
                Quantity_Limit = "N",
                Step_Therapy = "N"
            }
        };

        var sut = CreateService(HttpStatusCode.OK, responseData);

        var result = await sut.GetFormularyEntriesAsync("H1234", ["197361", "312961", "198211"]);

        Assert.Equal(3, result.Count);

        var lisinopril = result.First(e => e.RxCui == "197361");
        Assert.Equal("LISINOPRIL 10MG TABLET", lisinopril.DrugName);
        Assert.Equal(1, lisinopril.FormularyTier);
        Assert.False(lisinopril.RequiresPriorAuth);
        Assert.False(lisinopril.HasQuantityLimit);
        Assert.False(lisinopril.HasStepTherapy);

        var metformin = result.First(e => e.RxCui == "312961");
        Assert.True(metformin.HasQuantityLimit);

        var atorvastatin = result.First(e => e.RxCui == "198211");
        Assert.Equal(2, atorvastatin.FormularyTier);
    }

    [Fact]
    public async Task GetFormularyEntriesAsync_PriorAuthYes_ParsedCorrectly()
    {
        var responseData = new[]
        {
            new
            {
                Contract_Id = "H1234",
                Rxcui = "327361",
                Drug_Name = "ADALIMUMAB",
                Tier_Level = "5",
                Prior_Auth = "Y",
                Quantity_Limit = "Y",
                Step_Therapy = "Y"
            }
        };

        var sut = CreateService(HttpStatusCode.OK, responseData);

        var result = await sut.GetFormularyEntriesAsync("H1234", ["327361"]);

        Assert.Single(result);
        Assert.Equal(5, result[0].FormularyTier);
        Assert.True(result[0].RequiresPriorAuth);
        Assert.True(result[0].HasQuantityLimit);
        Assert.True(result[0].HasStepTherapy);
    }

    [Fact]
    public async Task GetFormularyEntriesAsync_EmptyContractId_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.OK, Array.Empty<object>());
        var result = await sut.GetFormularyEntriesAsync("", ["197361"]);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFormularyEntriesAsync_EmptyRxCuis_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.OK, Array.Empty<object>());
        var result = await sut.GetFormularyEntriesAsync("H1234", []);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFormularyEntriesAsync_ApiError_ReturnsEmpty()
    {
        var sut = CreateService(HttpStatusCode.ServiceUnavailable, null);

        var result = await sut.GetFormularyEntriesAsync("H1234", ["197361"]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFormularyEntriesAsync_CachesResults()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, new[]
        {
            new { Contract_Id = "H1234", Rxcui = "197361", Drug_Name = "LISINOPRIL",
                  Tier_Level = "1", Prior_Auth = "N", Quantity_Limit = "N", Step_Therapy = "N" }
        });

        var client = new HttpClient(handler);
        var sut = new CmsPlanDataService(client, _cache, _config, _loggerMock.Object);

        // First call
        var result1 = await sut.GetFormularyEntriesAsync("H1234", ["197361"]);
        // Second call (should be cached)
        var result2 = await sut.GetFormularyEntriesAsync("H1234", ["197361"]);

        Assert.Single(result1);
        Assert.Single(result2);
        Assert.Equal(1, handler.CallCount); // HTTP called only once
    }

    [Fact]
    public async Task GetFormularyEntriesAsync_UnparseableTier_DefaultsTo3()
    {
        var responseData = new[]
        {
            new
            {
                Contract_Id = "H1234",
                Rxcui = "197361",
                Drug_Name = "LISINOPRIL",
                Tier_Level = "N/A",
                Prior_Auth = "N",
                Quantity_Limit = "N",
                Step_Therapy = "N"
            }
        };

        var sut = CreateService(HttpStatusCode.OK, responseData);

        var result = await sut.GetFormularyEntriesAsync("H1234", ["197361"]);

        Assert.Single(result);
        Assert.Equal(3, result[0].FormularyTier); // Defaults to 3 when unparseable
    }

    // ═══════ Helpers ═══════

    private CmsPlanDataService CreateService(HttpStatusCode statusCode, object? responseData)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseData);
        var client = new HttpClient(handler);
        return new CmsPlanDataService(client, _cache, _config, _loggerMock.Object);
    }

    /// <summary>
    /// Mock HttpMessageHandler that returns canned JSON responses with call counting.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _responseJson;
        public int CallCount { get; private set; }

        public MockHttpMessageHandler(HttpStatusCode statusCode, object? responseData)
        {
            _statusCode = statusCode;
            _responseJson = responseData is not null
                ? JsonSerializer.Serialize(responseData)
                : null;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(_statusCode);
            if (_responseJson is not null)
            {
                response.Content = new StringContent(_responseJson, Encoding.UTF8, "application/json");
            }
            return Task.FromResult(response);
        }
    }
}
