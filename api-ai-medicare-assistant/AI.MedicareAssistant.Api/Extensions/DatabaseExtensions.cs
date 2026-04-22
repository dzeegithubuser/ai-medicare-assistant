using Domain.Interfaces;
using Infrastructure.Data;
using Infrastructure.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Api.Extensions;

internal static class DatabaseExtensions
{
    internal static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        var camelCasePack = new ConventionPack { new CamelCaseElementNameConvention() };
        ConventionRegistry.Register("CamelCase", camelCasePack, _ => true);
        
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

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
