namespace EnterpriseDataManager.Infrastructure;

using Microsoft.Extensions.Configuration;

public sealed class InfrastructureOptions
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
