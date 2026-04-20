using Domain.Interfaces;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Api.Extensions;

internal static class DatabaseExtensions
{
    internal static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // ------- MySQL + EF Core -------
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContextPool<AppDbContext>(options =>
            options.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString)));

        // ------- MongoDB client & database -------
        var mongoConnectionString = configuration.GetConnectionString("MongoDb")!;
        var mongoDatabaseName     = configuration["MongoDb:DatabaseName"] ?? "ai_medicare_assistant";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));
        services.AddSingleton<MongoDbContext>();
        services.AddHostedService<MongoIndexInitializer>();

        // ------- MongoDB repositories -------
        services.AddScoped<IUserAnalysisSelectionsRepository, UserAnalysisSelectionsRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        services.AddScoped<ILtcSelectionsRepository, LtcSelectionsRepository>();

        return services;
    }
}
