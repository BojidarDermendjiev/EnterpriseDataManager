namespace EnterpriseDataManager.Data;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Data.Interceptors;
using EnterpriseDataManager.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<EnterpriseDataManagerDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(EnterpriseDataManagerDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            var domainEventDispatcher = provider.GetService<IDomainEventDispatcher>();
            if (domainEventDispatcher is not null)
            {
                options.AddInterceptors(new DomainEventDispatchInterceptor(domainEventDispatcher));
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        services.AddScoped<IArchivePlanRepository, ArchivePlanRepository>();
        services.AddScoped<IArchiveJobRepository, ArchiveJobRepository>();
        services.AddScoped<IRecoveryJobRepository, RecoveryJobRepository>();
        services.AddScoped<IStorageProviderRepository, StorageProviderRepository>();
        services.AddScoped<IRetentionPolicyRepository, RetentionPolicyRepository>();
        services.AddScoped<IAuditRecordRepository, AuditRecordRepository>();
        services.AddScoped<IRepository<ArchiveItem>, GenericRepository<ArchiveItem>>();

        return services;
    }

    public static IServiceCollection AddDataLayerWithAuditing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<EnterpriseDataManagerDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(EnterpriseDataManagerDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            var domainEventDispatcher = provider.GetService<IDomainEventDispatcher>();
            if (domainEventDispatcher is not null)
            {
                options.AddInterceptors(new DomainEventDispatchInterceptor(domainEventDispatcher));
            }

            options.AddInterceptors(new AuditSaveChangesInterceptor());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        services.AddScoped<IArchivePlanRepository, ArchivePlanRepository>();
        services.AddScoped<IArchiveJobRepository, ArchiveJobRepository>();
        services.AddScoped<IRecoveryJobRepository, RecoveryJobRepository>();
        services.AddScoped<IStorageProviderRepository, StorageProviderRepository>();
        services.AddScoped<IRetentionPolicyRepository, RetentionPolicyRepository>();
        services.AddScoped<IAuditRecordRepository, AuditRecordRepository>();
        services.AddScoped<IRepository<ArchiveItem>, GenericRepository<ArchiveItem>>();

        return services;
    }
}
