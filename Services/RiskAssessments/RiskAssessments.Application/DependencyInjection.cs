using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RiskAssessments.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Outer → inner. Logging wraps validation so a rejected command is logged
            // before the handler runs. Performance sits next to the handler.
            cfg.AddOpenBehavior(typeof(Common.Behaviors.ExceptionHandlingBehavior<,>));
            cfg.AddOpenBehavior(typeof(Common.Behaviors.LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(Common.Behaviors.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Common.Behaviors.PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
