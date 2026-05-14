using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

/// <summary>
/// Rejects every authenticated action when the principal carries
/// <c>mustChangePassword=true</c>, except the password-change endpoint itself.
/// Anonymous endpoints (signin, forgot-password, etc.) pass through untouched.
/// </summary>
public class MustChangePasswordFilter : IAsyncActionFilter
{
    private const string ChangePasswordPath = "/api/auth/change-password";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var mustChange = principal.FindFirst("mustChangePassword")?.Value == "true";
            if (mustChange)
            {
                var path = context.HttpContext.Request.Path.Value ?? string.Empty;
                if (!path.Equals(ChangePasswordPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedException("Password change required before continuing.");
                }
            }
        }

        await next();
    }
}
