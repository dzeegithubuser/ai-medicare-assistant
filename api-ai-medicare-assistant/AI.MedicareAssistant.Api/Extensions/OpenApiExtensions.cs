using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Api.Extensions;

internal static class OpenApiExtensions
{
    internal static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((doc, ctx, ct) =>
            {
                doc.Info = new()
                {
                    Title       = "AI Medicare Assistant API",
                    Version     = "v1",
                    Description = "Medicare drug analysis, plan recommendation, and cost projection API."
                };
                doc.Components ??= new OpenApiComponents();
                doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type         = SecuritySchemeType.Http,
                    Scheme       = "bearer",
                    BearerFormat = "JWT",
                    Description  = "Paste your JWT token (without the 'Bearer ' prefix)."
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((op, ctx, ct) =>
            {
                var metadata = ctx.Description.ActionDescriptor.EndpointMetadata;
                var hasAuth  = metadata.OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any();
                var isAnon   = metadata.OfType<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>().Any();
                if (hasAuth && !isAnon)
                {
                    op.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("Bearer", ctx.Document)] = []
                        }
                    ];
                }
                return Task.CompletedTask;
            });
        });

        return services;
    }

    internal static void MapOpenApiEndpoints(this WebApplication app)
    {
        app.MapOpenApi("/openapi/v1.json");
        app.MapScalarApiReference(options =>
        {
            options.Title             = "AI Medicare Assistant API";
            options.Theme             = ScalarTheme.DeepSpace;
            options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
            options.Authentication    = new ScalarAuthenticationOptions
            {
                PreferredSecuritySchemes = ["Bearer"]
            };
        });
        app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
    }
}
