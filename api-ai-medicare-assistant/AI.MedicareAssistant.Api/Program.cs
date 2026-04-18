using Api.Extensions;
using Api.Hubs;
using Api.Middleware;
using Serilog;

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

    builder.AddSerilog();

    builder.Services.AddCoreServices(builder.Configuration);
    builder.Services.AddOpenApiDocumentation();
    builder.Services.AddDatabaseServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddEmailServices(builder.Configuration);
    builder.Services.AddAiProvider(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureHttpClients();

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
        app.MapOpenApiEndpoints();

    app.UseRequestLogging();
    app.UseCors();
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
