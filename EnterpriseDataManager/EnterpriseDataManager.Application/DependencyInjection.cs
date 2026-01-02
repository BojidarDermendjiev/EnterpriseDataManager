namespace EnterpriseDataManager.Application;

using EnterpriseDataManager.Application.Common.Behaviors;
using EnterpriseDataManager.Application.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        services.AddAutoMapper(assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IArchivalAppService, ArchivalAppService>();
        services.AddScoped<IRecoveryAppService, RecoveryAppService>();
        services.AddScoped<IAuditAppService, AuditAppService>();
        services.AddScoped<IStorageAppService, StorageAppService>();
        services.AddScoped<IPolicyAppService, PolicyAppService>();

        return services;
    }
}
