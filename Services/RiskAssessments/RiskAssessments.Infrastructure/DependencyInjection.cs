using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskAssessments.Application.Common.Interfaces;
using RiskAssessments.Domain.Interfaces;
using RiskAssessments.Infrastructure.Indexing;
using RiskAssessments.Infrastructure.Persistence;
using RiskAssessments.Infrastructure.Seeding;

namespace RiskAssessments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();

        services.Configure<MongoDbOptions>(config.GetSection(MongoDbOptions.SectionName));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IRiskAssessmentRepository, RiskAssessmentRepository>();
        services.AddScoped<IAuditNoteRepository, AuditNoteRepository>();
        services.AddScoped<IRiskAssessmentQueries, RiskAssessmentQueries>();
        services.AddScoped<IAuditNoteQueries, AuditNoteQueries>();

        services.AddSingleton<IndexCreationService>();
        services.AddSingleton<IIndexCreationService>(sp => sp.GetRequiredService<IndexCreationService>());
        services.AddHostedService(sp => sp.GetRequiredService<IndexCreationService>());

        services.AddSingleton<IDataSeeder, AnomalyRuleSeeder>();
        services.AddSingleton<IDataSeeder, RiskAssessmentSeeder>();
        services.AddSingleton<IDataSeeder, AuditNoteSeeder>();

        return services;
    }
}
