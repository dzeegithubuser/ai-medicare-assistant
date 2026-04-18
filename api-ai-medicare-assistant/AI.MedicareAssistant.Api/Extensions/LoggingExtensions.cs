using MongoDB.Driver;
using Serilog;

namespace Api.Extensions;

internal static class LoggingExtensions
{
    internal static void AddSerilog(this WebApplicationBuilder builder)
    {
        var mongoConnStr = builder.Configuration.GetConnectionString("MongoDb")!;
        var mongoDbName  = builder.Configuration["MongoDb:DatabaseName"] ?? "ai_medicare_assistant";

        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AI.MedicareAssistant")
            .WriteTo.MongoDBBson(cfg =>
            {
                var mongoClient = new MongoClient(mongoConnStr);
                cfg.SetMongoDatabase(mongoClient.GetDatabase(mongoDbName));
                cfg.SetCollectionName("logs");
                cfg.SetBatchPeriod(TimeSpan.FromSeconds(5));
            })
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File("Logs/log-.txt",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                retainedFileCountLimit: 30));
    }

    internal static void UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "");
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString() ?? "");
            };
        });
    }
}
