using EnterpriseDataManager.Infrastructure;
using EnterpriseDataManager.Infrastructure.Identity;
using EnterpriseDataManager.Infrastructure.Identity.IdamConnectors;
using EnterpriseDataManager.Infrastructure.Identity.MfaProviders;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseDataManager.IntegrationTests.Identity;

public class DependencyInjectionTests
{
    [Fact]
    public void AddIdentityServices_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IMfaStateStore>().Should().NotBeNull();
        provider.GetService<IMfaProvider>().Should().NotBeNull();
        provider.GetService<IIdentityService>().Should().NotBeNull();
    }

    [Fact]
    public void AddIdentityServices_WithLdapConfig_ShouldRegisterLdapConnector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Ldap:Server"] = "ldap.example.com",
                ["Identity:Ldap:Port"] = "389",
                ["Identity:Ldap:BaseDn"] = "dc=example,dc=com",
                ["Identity:Ldap:BindDn"] = "cn=admin,dc=example,dc=com",
                ["Identity:Ldap:BindPassword"] = "password"
            })
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var connectors = provider.GetServices<IIdamConnector>();
        connectors.Should().ContainSingle(c => c.ProviderName == "LDAP");
    }

    [Fact]
    public void AddIdentityServices_WithOidcConfig_ShouldRegisterOidcConnector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();
        services.AddHttpClient();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Oidc:Authority"] = "https://auth.example.com",
                ["Identity:Oidc:ClientId"] = "client-id",
                ["Identity:Oidc:ClientSecret"] = "client-secret",
                ["Identity:Oidc:RedirectUri"] = "https://app.example.com/callback"
            })
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var connectors = provider.GetServices<IIdamConnector>();
        connectors.Should().ContainSingle(c => c.ProviderName == "OIDC");
    }

    [Fact]
    public void AddIdentityServices_WithBothConfigs_ShouldRegisterBothConnectors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();
        services.AddHttpClient();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Ldap:Server"] = "ldap.example.com",
                ["Identity:Ldap:Port"] = "389",
                ["Identity:Ldap:BaseDn"] = "dc=example,dc=com",
                ["Identity:Ldap:BindDn"] = "cn=admin",
                ["Identity:Ldap:BindPassword"] = "password",
                ["Identity:Oidc:Authority"] = "https://auth.example.com",
                ["Identity:Oidc:ClientId"] = "client-id",
                ["Identity:Oidc:ClientSecret"] = "client-secret"
            })
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var connectors = provider.GetServices<IIdamConnector>().ToList();
        connectors.Should().HaveCount(2);
        connectors.Should().Contain(c => c.ProviderName == "LDAP");
        connectors.Should().Contain(c => c.ProviderName == "OIDC");
    }

    [Fact]
    public void AddIdentityServices_ShouldConfigureTotpOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Mfa:Totp:Issuer"] = "MyApp",
                ["Identity:Mfa:Totp:CodeLength"] = "8",
                ["Identity:Mfa:Totp:MaxFailedAttempts"] = "5"
            })
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TotpOptions>>();
        options.Value.Issuer.Should().Be("MyApp");
        options.Value.CodeLength.Should().Be(8);
        options.Value.MaxFailedAttempts.Should().Be(5);
    }

    [Fact]
    public void AddIdentityServices_ShouldConfigureIdentityOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:TokenSigningKey"] = "CustomSigningKey_32characters!!",
                ["Identity:TokenIssuer"] = "CustomIssuer",
                ["Identity:TokenAudience"] = "CustomAudience",
                ["Identity:RequireMfa"] = "true"
            })
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
        options.Value.TokenSigningKey.Should().Be("CustomSigningKey_32characters!!");
        options.Value.TokenIssuer.Should().Be("CustomIssuer");
        options.Value.TokenAudience.Should().Be("CustomAudience");
        options.Value.RequireMfa.Should().BeTrue();
    }

    [Fact]
    public void AddInfrastructure_ShouldIncludeIdentityServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IIdentityService>().Should().NotBeNull();
        provider.GetService<IMfaProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructureWithOptions_ShouldRespectEnableIdentityFlag()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act - Disable identity
        services.AddInfrastructureWithOptions(options =>
        {
            options.Configuration = configuration;
            options.EnableIdentity = false;
            options.EnableStorage = false;
            options.EnableSecurity = false;
            options.EnableNotifications = false;
            options.EnableBackgroundJobs = false;
            options.EnableLogging = false;
            options.EnableEventDispatcher = false;
        });

        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IIdentityService>().Should().BeNull();
    }

    [Fact]
    public void InMemoryMfaStateStore_ShouldBeRegisteredAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var store1 = provider.GetService<IMfaStateStore>();
        var store2 = provider.GetService<IMfaStateStore>();
        store1.Should().BeSameAs(store2);
    }

    [Fact]
    public void TotpMfaProvider_ShouldBeRegisteredAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddIdentityServices(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var mfa1 = provider.GetService<IMfaProvider>();
        var mfa2 = provider.GetService<IMfaProvider>();
        mfa1.Should().BeSameAs(mfa2);
    }
}
