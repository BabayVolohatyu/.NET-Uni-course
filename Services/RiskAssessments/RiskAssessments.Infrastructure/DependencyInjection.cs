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

        // Lab 05: Support Aspire connection string injection.
        // When running under AppHost, Aspire injects ConnectionStrings:RiskAssessmentDb
        // as an environment variable. Fall back to the MongoDb section for standalone runs.
        var aspireConnectionString = config.GetConnectionString("RiskAssessmentDb");
        if (!string.IsNullOrWhiteSpace(aspireConnectionString))
        {
            // Aspire-injected connection string — override MongoDb config section
            services.Configure<MongoDbOptions>(o =>
            {
                o.ConnectionString = aspireConnectionString;
                // Keep the DatabaseName from config (or use a default)
                var dbName = config["MongoDb:DatabaseName"];
                o.DatabaseName = string.IsNullOrWhiteSpace(dbName) ? "RiskAssessment" : dbName;
            });
        }
        else
        {
            // Standalone run (without Aspire) — read from MongoDb config section as before
            services.Configure<MongoDbOptions>(config.GetSection(MongoDbOptions.SectionName));
        }
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
