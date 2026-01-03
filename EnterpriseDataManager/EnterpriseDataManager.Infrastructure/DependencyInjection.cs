namespace EnterpriseDataManager.Infrastructure;

using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage.Blobs;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Infrastructure.BackgroundJobs;
using EnterpriseDataManager.Infrastructure.Events;
using EnterpriseDataManager.Infrastructure.ExternalServices;
using EnterpriseDataManager.Infrastructure.Identity;
using EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;
using EnterpriseDataManager.Infrastructure.Identity.MfaProviders;
using EnterpriseDataManager.Infrastructure.Logging;
using EnterpriseDataManager.Infrastructure.Security.Encryption;
using EnterpriseDataManager.Infrastructure.Security.NetworkSecurity;
using EnterpriseDataManager.Infrastructure.Security.RansomwareProtection;
using EnterpriseDataManager.Infrastructure.Storage;
using EnterpriseDataManager.Infrastructure.Storage.TapeDevice;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Storage providers
        services.AddStorageServices(configuration);

        // Security services
        services.AddSecurityServices(configuration);

        // Network security services
        services.AddNetworkSecurityServices(configuration);

        // Ransomware protection services
        services.AddRansomwareProtectionServices(configuration);

        // Notification services
        services.AddNotificationServices(configuration);

        // Background job services
        services.AddBackgroundJobServices(configuration);

        // Logging services
        services.AddLoggingServices(configuration);

        // Domain event dispatcher
        services.AddEventDispatcher(configuration);

        // Identity services
        services.AddIdentityServices(configuration);

        // HTTP client factory
        services.AddHttpClient();

        return services;
    }

    public static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Local filesystem provider
        services.AddSingleton<LocalFilesystemProvider>();

        // S3 provider (optional based on configuration)
        var s3Section = configuration.GetSection("Storage:S3");
        if (s3Section.Exists())
        {
            //services.AddSingleton<S3CompatibleProvider>(sp =>
            //{
            //    var accessKey = s3Section.GetValue<string>("AccessKey") ?? "";
            //    var secretKey = s3Section.GetValue<string>("SecretKey") ?? "";
            //    var bucketName = s3Section.GetValue<string>("BucketName") ?? "";
            //    var endpoint = s3Section.GetValue<string>("Endpoint");
            //    var region = s3Section.GetValue<string>("Region") ?? "us-east-1";

            //    return new S3CompatibleProvider(
            //        accessKey, secretKey, bucketName,
            //        endpoint: endpoint, region: region,
            //        logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<S3CompatibleProvider>>());
            //});

            services.AddSingleton<S3CompatibleProvider>(sp =>
            {
                var accessKey = s3Section.GetValue<string>("AccessKey") ?? "";
                var secretKey = s3Section.GetValue<string>("SecretKey") ?? "";
                var bucketName = s3Section.GetValue<string>("BucketName") ?? "";
                var endpoint = s3Section.GetValue<string>("Endpoint");
                var region = s3Section.GetValue<string>("Region") ?? "us-east-1";
                var prefix = s3Section.GetValue<string>("Prefix");

                // Build AWS credentials and S3 client
                var credentials = new BasicAWSCredentials(accessKey, secretKey);

                AmazonS3Config s3Config;
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    // For S3-compatible providers (e.g., MinIO)
                    s3Config = new AmazonS3Config
                    {
                        ServiceURL = endpoint,
                        ForcePathStyle = s3Section.GetValue<bool?>("ForcePathStyle") ?? true
                    };
                }
                else
                {
                    s3Config = new AmazonS3Config
                    {
                        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
                    };
                }

                var s3Client = new AmazonS3Client(credentials, s3Config);

                // Match constructor: (string accessKey, IAmazonS3 s3Client, string bucketName, string? prefix = null)
                return new S3CompatibleProvider(accessKey, s3Client, bucketName, prefix);
            });

        }

        // Azure provider (optional based on configuration)
        var azureSection = configuration.GetSection("Storage:AzureBlob");
        if (azureSection.Exists())
        {
            //services.AddSingleton<AzureBlobProvider>(sp =>
            //{
            //    var connectionString = azureSection.GetValue<string>("ConnectionString") ?? "";
            //    var containerName = azureSection.GetValue<string>("ContainerName") ?? "default";

            //    return new AzureBlobProvider(
            //        connectionString, containerName,
            //        logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<AzureBlobProvider>>());
            //});

            services.AddSingleton<AzureBlobProvider>(sp =>
            {
                var connectionString = azureSection.GetValue<string>("ConnectionString") ?? "";
                var containerName = azureSection.GetValue<string>("ContainerName") ?? "default";
                var prefix = azureSection.GetValue<string>("Prefix");

                // Build BlobContainerClient and pass to provider
                var containerClient = new BlobContainerClient(connectionString, containerName);

                // Match constructor: (BlobContainerClient containerClient, string? prefix = null)
                return new AzureBlobProvider(containerClient, prefix);
            });
        }

        // Tape device adapter (mock by default)
        services.AddSingleton<ITapeDeviceAdapter, MockTapeAdapter>();

        return services;
    }

    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Encryption service
        var encryptionSection = configuration.GetSection(EncryptionOptions.SectionName);
        if (encryptionSection.Exists())
        {
            services.Configure<EncryptionOptions>(encryptionSection);
        }
        else
        {
            services.Configure<EncryptionOptions>(options => { });
        }
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        return services;
    }

    public static IServiceCollection AddNetworkSecurityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Firewall rule service
        services.AddSingleton<IFirewallManager, FirewallRuleService>(sp =>
            new FirewallRuleService(
                logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<FirewallRuleService>>()));

        // VPN config service
        var vpnSection = configuration.GetSection(VpnOptions.SectionName);
        if (vpnSection.Exists())
        {
            services.Configure<VpnOptions>(vpnSection);
            services.AddSingleton<VpnConfigService>();
        }

        return services;
    }

    public static IServiceCollection AddRansomwareProtectionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Anomaly detector
        services.AddSingleton<AnomalyDetector>(sp =>
            new AnomalyDetector(
                options: null,
                logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<AnomalyDetector>>()));

        // Immutable storage
        var storagePath = configuration.GetValue<string>("RansomwareProtection:ImmutableStoragePath")
            ?? Path.Combine(Path.GetTempPath(), "edm-immutable");
        services.AddSingleton<ImmutableStorageService>(sp =>
            new ImmutableStorageService(storagePath,
                logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<ImmutableStorageService>>()));

        // WORM simulator
        services.AddSingleton<WormSimulator>();

        return services;
    }

    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Email service
        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        if (emailSection.Exists())
        {
            services.Configure<EmailOptions>(emailSection);
        }
        else
        {
            services.Configure<EmailOptions>(options => { });
        }
        services.AddSingleton<IEmailService, EmailService>();

        // Notification service
        var notificationSection = configuration.GetSection(NotificationOptions.SectionName);
        if (notificationSection.Exists())
        {
            services.Configure<NotificationOptions>(notificationSection);
        }
        else
        {
            services.Configure<NotificationOptions>(options => { });
        }
        services.AddSingleton<ExternalServices.INotificationService, NotificationService>();

        return services;
    }

    public static IServiceCollection AddBackgroundJobServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Archival job scheduler
        var schedulerSection = configuration.GetSection(ArchivalJobSchedulerOptions.SectionName);
        if (schedulerSection.Exists())
        {
            services.Configure<ArchivalJobSchedulerOptions>(schedulerSection);
        }
        else
        {
            services.Configure<ArchivalJobSchedulerOptions>(options => { });
        }
        services.AddSingleton<IArchivalJobScheduler, ArchivalJobScheduler>();
        services.AddHostedService(sp => (ArchivalJobScheduler)sp.GetRequiredService<IArchivalJobScheduler>());

        // Retention policy enforcer
        var retentionSection = configuration.GetSection(RetentionPolicyEnforcerOptions.SectionName);
        if (retentionSection.Exists())
        {
            services.Configure<RetentionPolicyEnforcerOptions>(retentionSection);
        }
        else
        {
            services.Configure<RetentionPolicyEnforcerOptions>(options => { });
        }
        services.AddSingleton<IRetentionPolicyEnforcer, RetentionPolicyEnforcer>();
        services.AddHostedService(sp => (RetentionPolicyEnforcer)sp.GetRequiredService<IRetentionPolicyEnforcer>());

        // Health check monitor
        var healthSection = configuration.GetSection(HealthCheckMonitorOptions.SectionName);
        if (healthSection.Exists())
        {
            services.Configure<HealthCheckMonitorOptions>(healthSection);
        }
        else
        {
            services.Configure<HealthCheckMonitorOptions>(options => { });
        }
        services.AddSingleton<IHealthCheckMonitor, HealthCheckMonitor>();
        services.AddHostedService(sp => (HealthCheckMonitor)sp.GetRequiredService<IHealthCheckMonitor>());

        return services;
    }

    public static IServiceCollection AddLoggingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Syslog forwarder
        var syslogSection = configuration.GetSection(SyslogForwarderOptions.SectionName);
        if (syslogSection.Exists())
        {
            services.Configure<SyslogForwarderOptions>(syslogSection);
            services.AddSingleton<ISiemForwarder, SyslogForwarder>();
        }

        // CEF forwarder (alternative to syslog)
        var cefSection = configuration.GetSection(CefForwarderOptions.SectionName);
        if (cefSection.Exists() && !syslogSection.Exists())
        {
            services.Configure<CefForwarderOptions>(cefSection);
            services.AddSingleton<ISiemForwarder, CefForwarder>();
        }

        // Audit logger
        services.AddSingleton<IAuditLogger, InMemoryAuditLogger>();

        return services;
    }

    public static IServiceCollection AddEventDispatcher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }

    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Identity options
        var identitySection = configuration.GetSection(IdentityOptions.SectionName);
        if (identitySection.Exists())
        {
            services.Configure<IdentityOptions>(identitySection);
        }
        else
        {
            services.Configure<IdentityOptions>(options => { });
        }

        // MFA State Store (in-memory by default)
        services.AddSingleton<IMfaStateStore, InMemoryMfaStateStore>();

        // TOTP MFA Provider options
        var totpSection = configuration.GetSection(TotpOptions.SectionName);
        if (totpSection.Exists())
        {
            services.Configure<TotpOptions>(totpSection);
        }
        else
        {
            services.Configure<TotpOptions>(options => { });
        }
        services.AddSingleton<IMfaProvider, TotpMfaProvider>();

        // LDAP Connector (optional based on configuration)
        var ldapSection = configuration.GetSection(LdapOptions.SectionName);
        if (ldapSection.Exists())
        {
            services.Configure<LdapOptions>(ldapSection);
            services.AddSingleton<IIdamConnector, LdapConnector>();
        }

        // OIDC Connector (optional based on configuration)
        var oidcSection = configuration.GetSection(OidcOptions.SectionName);
        if (oidcSection.Exists())
        {
            services.Configure<OidcOptions>(oidcSection);
            services.AddSingleton<IIdamConnector>(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OidcOptions>>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<OidcConnector>>();

                return new OidcConnector(
                    options,
                    httpClientFactory.CreateClient("OidcConnector"),
                    cache,
                    logger);
            });
        }

        // Identity Service
        services.AddSingleton<IIdentityService, IdentityService>();

        return services;
    }

    public static IServiceCollection AddInfrastructureWithOptions(
        this IServiceCollection services,
        Action<InfrastructureOptions> configureOptions)
    {
        var options = new InfrastructureOptions();
        configureOptions(options);

        if (options.EnableStorage)
        {
            services.AddStorageServices(options.Configuration!);
        }

        if (options.EnableSecurity)
        {
            services.AddSecurityServices(options.Configuration!);
            services.AddNetworkSecurityServices(options.Configuration!);
            services.AddRansomwareProtectionServices(options.Configuration!);
        }

        if (options.EnableNotifications)
        {
            services.AddNotificationServices(options.Configuration!);
        }

        if (options.EnableBackgroundJobs)
        {
            services.AddBackgroundJobServices(options.Configuration!);
        }

        if (options.EnableLogging)
        {
            services.AddLoggingServices(options.Configuration!);
        }

        if (options.EnableEventDispatcher)
        {
            services.AddEventDispatcher(options.Configuration!);
        }

        if (options.EnableIdentity)
        {
            services.AddIdentityServices(options.Configuration!);
        }

        if (options.EnableHttpClient)
        {
            services.AddHttpClient();
        }

        return services;
    }
}

public class InfrastructureOptions
{
    public IConfiguration? Configuration { get; set; }
    public bool EnableStorage { get; set; } = true;
    public bool EnableSecurity { get; set; } = true;
    public bool EnableNotifications { get; set; } = true;
    public bool EnableBackgroundJobs { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
    public bool EnableEventDispatcher { get; set; } = true;
    public bool EnableIdentity { get; set; } = true;
    public bool EnableHttpClient { get; set; } = true;
}
