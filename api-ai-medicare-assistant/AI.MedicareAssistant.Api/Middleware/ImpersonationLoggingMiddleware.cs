using Serilog.Context;

namespace Api.Middleware;

/// <summary>
/// When the current principal carries an <c>actingAs</c> claim (impersonation token),
/// pushes <c>ImpersonatedBy</c> into the Serilog log context for the duration of the
/// request so every log line emitted while impersonating records the FP's user id.
/// Must be registered AFTER <see cref="Microsoft.AspNetCore.Builder.AuthAppBuilderExtensions.UseAuthentication"/>
/// so the principal is populated.
/// </summary>
public class ImpersonationLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public ImpersonationLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var actingAs = context.User.FindFirst("actingAs")?.Value;
        if (!string.IsNullOrEmpty(actingAs))
        {
            using (LogContext.PushProperty("ImpersonatedBy", actingAs))
            {
                await _next(context);
            }
            return;
        }

        await _next(context);
    }
}
