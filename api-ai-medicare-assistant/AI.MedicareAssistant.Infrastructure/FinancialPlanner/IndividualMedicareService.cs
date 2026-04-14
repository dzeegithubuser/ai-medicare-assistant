using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Infrastructure.FinancialPlanner;

public class IndividualMedicareService : IIndividualMedicareService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IndividualMedicareService> _logger;
    private readonly string _baseUrl;
    private readonly string _authToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IndividualMedicareService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IndividualMedicareService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["FinancialPlanner:BaseUrl"]
            ?? "http://169.61.105.110:8080/NewFinancialPlanner/api/v1";
        _authToken = configuration["FinancialPlanner:AuthToken"] ?? "";
    }

    public async Task<IndividualMedicareResponse> CalculateAsync(
        IndividualMedicareRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calling individualMedicareR5 for email={Email}, coverageYear={Year}, planBundle={Bundle}",
            request.UserEmail, request.CoverageYear, request.PlanBundleCode);
        request.RetirementYear = "03-2026";


        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/individualMedicareR5");

        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        if (!string.IsNullOrEmpty(_authToken))
            httpRequest.Headers.TryAddWithoutValidation("Authorization", _authToken);

        var requestBody = await httpRequest.Content!.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP individualMedicareR5] Request: {Body}", requestBody);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("[FP individualMedicareR5] Response ({StatusCode}): {Body}",
            (int)response.StatusCode, responseBody);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<IndividualMedicareResponse>(responseBody, JsonOptions);

        if (result is null)
        {
            _logger.LogWarning("individualMedicareR5 returned null response");
            return new IndividualMedicareResponse { WebServiceStatus = "EMPTY_RESPONSE" };
        }

        _logger.LogInformation(
            "individualMedicareR5 completed: status={Status}, transactionId={TxId}, yearCount={Years}",
            result.WebServiceStatus, result.WebServiceTransactionId, result.IndividualMedicares.Count);

        return result;
    }
}

public static class HttpRequestMessageExtensions
{
    public static async Task<string> ToCurlAsync(this HttpRequestMessage request)
    {
        var sb = new StringBuilder();

        // Method + URL
        sb.Append($"curl -X {request.Method.Method} \"{request.RequestUri}\"");

        // Headers
        foreach (var header in request.Headers)
        {
            foreach (var value in header.Value)
                sb.Append($" \\\n  -H \"{header.Key}: {value}\"");
        }

        // Content headers + body
        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                foreach (var value in header.Value)
                    sb.Append($" \\\n  -H \"{header.Key}: {value}\"");
            }

            var body = await request.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(body))
            {
                var escaped = body.Replace("'", "'\\''");
                sb.Append($" \\\n  -d '{escaped}'");
            }
        }

        return sb.ToString();
    }
}
