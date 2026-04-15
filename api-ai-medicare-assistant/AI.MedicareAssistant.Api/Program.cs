using Api.Hubs;
using Api.Middleware;
using Application.Services.Pipeline;
using Domain.Interfaces;
using Domain.Models;
using Infrastructure.AI;
using Infrastructure.Anthropic;
using Infrastructure.CountyLookup;
using Infrastructure.Data;
using Infrastructure.Fda;
using Infrastructure.Medicare;
using Infrastructure.Pharmacy;
using Infrastructure.Repositories;
using Infrastructure.RxNorm;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using OpenAI;
using Scalar.AspNetCore;
using Serilog;
using System.Text;

// ------- Serilog Bootstrap (file + console only – MongoDB not available yet) -------
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        retainedFileCountLimit: 30)
    .CreateBootstrapLogger();

try
{

    var builder = WebApplication.CreateBuilder(args);

    // Ensure relative paths (e.g. Prompts/) resolve against the API project directory
    Directory.SetCurrentDirectory(builder.Environment.ContentRootPath);

    // Replace default logging with Serilog
    var mongoConnStr = builder.Configuration.GetConnectionString("MongoDb")!;
    var mongoDbName  = builder.Configuration["MongoDb:DatabaseName"] ?? "ai_medicare_assistant";

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "AI.MedicareAssistant")
        // Primary sink – MongoDB (structured, queryable)
        .WriteTo.MongoDBBson(cfg =>
        {
            var mongoClient = new MongoDB.Driver.MongoClient(mongoConnStr);
            cfg.SetMongoDatabase(mongoClient.GetDatabase(mongoDbName));
            cfg.SetCollectionName("logs");
            cfg.SetBatchPeriod(TimeSpan.FromSeconds(5));
        })
        // Console sink – development convenience
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        // Fallback sink – rolling file
        .WriteTo.File("Logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            retainedFileCountLimit: 30));

    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddMemoryCache();

    // ------- OpenAPI / Scalar -------
    builder.Services.AddOpenApi("v1", options =>
    {
        options.AddDocumentTransformer((doc, ctx, ct) =>
        {
            doc.Info = new()
            {
                Title   = "AI Medicare Assistant API",
                Version = "v1",
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
    BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    // ------- MySQL + EF Core -------
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContextPool<AppDbContext>(options =>
        options.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString)));

    // ------- MongoDB -------
    var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb")!;
    var mongoDatabaseName = builder.Configuration["MongoDb:DatabaseName"] ?? "ai_medicare_assistant";
    builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));
    builder.Services.AddSingleton<MongoDbContext>();
    builder.Services.AddHostedService<MongoIndexInitializer>();
    builder.Services.AddScoped<IPrescriptionDocRepository, PrescriptionDocRepository>();
    builder.Services.AddScoped<IUserAnalysisSelectionsRepository, UserAnalysisSelectionsRepository>();
    builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
    builder.Services.AddScoped<IConvStateRepository, ConvStateRepository>();
    builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
    builder.Services.AddScoped<ILtcSelectionsRepository, LtcSelectionsRepository>();

    // ------- JWT Authentication -------
    var jwtSecret = builder.Configuration["Jwt:Secret"]!;
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        // SignalR WebSocket connections send the JWT as a query-string parameter
        // because browsers cannot set headers on WebSocket upgrade requests.
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.Request.Path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
    builder.Services.AddAuthorization();

    // ------- Repositories & Services -------
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
    builder.Services.AddScoped<Application.Services.AuthService>();
    builder.Services.AddScoped<Application.Services.ProfileService>();
    builder.Services.AddScoped<Application.Services.PrescriptionService>();
    builder.Services.AddScoped<Application.Services.RecommendationService>();

    // ------- AI Provider (switch via "AiProvider" setting: "OpenAI" or "Anthropic") -------
    var aiProvider = builder.Configuration["AiProvider"] ?? "Anthropic";

    if (aiProvider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.Configure<AnthropicOptions>(
            builder.Configuration.GetSection("Anthropic"));

        builder.Services.AddHttpClient<IChatClient, AnthropicMeaiChatClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>()
                           .GetSection("Anthropic")
                           .Get<AnthropicOptions>();

            client.BaseAddress = new Uri(config?.BaseUrl ?? "https://api.anthropic.com/v1/");
        });

        Log.Information("AI provider: Anthropic ({Model})",
            builder.Configuration["Anthropic:Model"] ?? "claude-sonnet-4-20250514");
    }
    else
    {
        var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]!;
        var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-4o";

        var innerChatClient = new OpenAI.Chat.ChatClient(openAiModel, openAiApiKey).AsIChatClient();

        builder.Services.AddChatClient(innerChatClient)
            .UseFunctionInvocation();

        Log.Information("AI provider: OpenAI ({Model})", openAiModel);
    }

    builder.Services.AddSingleton<Infrastructure.AI.PromptBuilder>();
    builder.Services.AddScoped<IDrugAiService, DrugAiService>();
    builder.Services.AddScoped<Application.Services.DrugAnalysisService>();

    // ------- Drug Analysis Pipeline Steps -------
    builder.Services.AddScoped<IDrugAnalysisStep, AiAnalysisStep>();
    builder.Services.AddScoped<IDrugAnalysisStep, DrugValidationStep>();
    builder.Services.AddScoped<IDrugAnalysisStep, CmsRxNormEnrichmentStep>();
    builder.Services.AddScoped<IDrugAnalysisStep, InteractionMergingStep>();

    // ------- CMS Medicare API -------
    builder.Services.AddHttpClient<IMedicareCostService, CmsMedicareCostService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // ------- RxNorm API -------
    builder.Services.AddHttpClient<IRxNormService, RxNormService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // ------- FDA NDC Directory API -------
    builder.Services.AddHttpClient<IFdaNdcService, FdaNdcService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // ------- Pharmacy Pricing (NPI + RxNav NDC) -------
    builder.Services.AddHttpClient<IPharmacyPricingService, CmsPharmacyPricingService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    // ------- Pharmacy Lookup (Financial Planner API) -------
    builder.Services.AddHttpClient<IPharmacyLookupService, Infrastructure.Pharmacy.FinancialPlannerPharmacyService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    // ------- County Code Lookup (Financial Planner API) -------
    builder.Services.AddHttpClient<ICountyLookupService, CountyLookupService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddScoped<IPlanScoringAiService, PlanScoringAiService>();
    builder.Services.AddHttpClient<ICmsPlanDataService, CmsPlanDataService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddScoped<Application.Services.MedicarePlanService>();
    builder.Services.AddScoped<IPlanPharmacyService, Application.Services.PlanPharmacyService>();
    builder.Services.AddScoped<ICostEvaluationAiService, Infrastructure.AI.CostEvaluationAiService>();
    builder.Services.AddScoped<ILtcEvaluationAiService, Infrastructure.AI.LtcEvaluationAiService>();
    builder.Services.AddScoped<Application.Services.CostProjectionService>();
    builder.Services.AddScoped<Application.Services.ChatIntentService>();
    builder.Services.AddScoped<Application.Services.ProfileExtractService>();
    builder.Services.AddScoped<Application.Services.DrugSelectionExtractService>();
    builder.Services.AddScoped<Application.Services.PharmacySelectionExtractService>();
    builder.Services.AddScoped<Application.Services.PlanSelectionExtractService>();
    builder.Services.AddScoped<Application.Services.ConvStateService>();
    builder.Services.AddScoped<Application.Services.OrchestratorIntentService>();
    builder.Services.AddScoped<Application.Services.ChatOrchestratorService>();
    builder.Services.AddScoped<Application.Services.DeltaCalculationService>();
    builder.Services.AddScoped<Application.Services.ChatSessionService>();

    // ------- Financial Planner Constants API -------
    builder.Services.AddHttpClient<IConstantsService, Infrastructure.FinancialPlanner.FinancialPlannerConstantsService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // ------- Financial Planner Individual Medicare API -------
    builder.Services.AddHttpClient<IIndividualMedicareService, Infrastructure.FinancialPlanner.IndividualMedicareService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // ------- Financial Planner Drug Search API -------
    builder.Services.AddHttpClient<IFinancialPlannerDrugService, Infrastructure.FinancialPlanner.FinancialPlannerDrugService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    // ------- Part D Plan Recommendation API -------
    builder.Services.AddHttpClient<IPartDPlanRecommendationService, Infrastructure.FinancialPlanner.PartDPlanRecommendationService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // ------- Medigap Plan Quotes API -------
    builder.Services.AddHttpClient<IMedigapPlanQuotesService, Infrastructure.FinancialPlanner.MedigapPlanQuotesService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // ------- Medicare Advantage Plan Recommendation API -------
    builder.Services.AddHttpClient<IMedicareAdvantagePlanService, Infrastructure.FinancialPlanner.MedicareAdvantagePlanService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // ------- Long Term Care API -------
    builder.Services.AddHttpClient<ILongTermCareService, Infrastructure.FinancialPlanner.LongTermCareService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // ------- Present Value API -------
    builder.Services.AddHttpClient<IPresentValueService, Infrastructure.FinancialPlanner.PresentValueService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    var app = builder.Build();

    // ------- Middleware Pipeline -------
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // ------- OpenAPI / Scalar (Development only) -------
    if (app.Environment.IsDevelopment())
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
        // Redirect root → Scalar UI so navigating to localhost:5024 opens the docs
        app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
    }

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "");
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString() ?? "");
        };
    });

    app.UseCors(options =>
    {
        // AllowCredentials() is required for SignalR WebSocket upgrade handshake.
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["http://localhost:4200", "http://169.61.105.110:9600"];
        options.WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
