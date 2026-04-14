using System.Text.Json;
using Domain.Exceptions;

namespace Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var (statusCode, message, errors) = exception switch
        {
            ValidationException ve => (ve.StatusCode, ve.Message, ve.Errors),
            NotFoundException nf   => (nf.StatusCode, nf.Message, (IDictionary<string, string[]>?)null),
            UnauthorizedException  => (401, exception.Message, null),
            ConflictException      => (409, exception.Message, null),
            AppException ae        => (ae.StatusCode, ae.Message, null),
            _                      => (500, "An unexpected error occurred.", null)
        };

        // Log with severity according to status code
        if (statusCode >= 500)
        {
            _logger.LogError(exception,
                "Unhandled exception | TraceId={TraceId} Path={Path} Method={Method}",
                traceId, context.Request.Path, context.Request.Method);
        }
        else
        {
            _logger.LogWarning(
                "Handled exception | TraceId={TraceId} Path={Path} Method={Method} Status={StatusCode} Message={Message}",
                traceId, context.Request.Path, context.Request.Method, statusCode, exception.Message);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            Status = statusCode,
            Message = message,
            TraceId = traceId,
            Errors = errors
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }
}

public class ErrorResponse
{
    public int Status { get; set; }
    public string Message { get; set; } = "";
    public string TraceId { get; set; } = "";
    public IDictionary<string, string[]>? Errors { get; set; }
}
