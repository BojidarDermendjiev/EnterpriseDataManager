namespace EnterpriseDataManager.IntegrationTests.Helpers;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnterpriseDataManager.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

public class TestWebApplicationFactory : WebApplicationFactory<WebApplicationEntryPoint>, IDisposable
{
    private const string TestJwtSecret = "test-secret-key-for-integration-tests-32c";
    private const string TestIssuer = "EnterpriseDataManager";
    private const string TestAudience = "EnterpriseDataManager";

    private readonly string _dbPath;

    public TestWebApplicationFactory()
    {
        _dbPath = Path.ChangeExtension(Path.GetTempFileName(), ".db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:ExpiryMinutes"] = "60",
                ["Seq:ServerUrl"] = "",
                ["ArchivalJobScheduler:Enabled"] = "false",
                ["RetentionPolicyEnforcer:Enabled"] = "false",
                ["HealthCheckMonitor:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var dbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<EnterpriseDataManagerDbContext>)
                         || d.ServiceType == typeof(EnterpriseDataManagerDbContext))
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Register SQLite-backed DbContext pointing at our temp file
            services.AddDbContext<EnterpriseDataManagerDbContext>(options =>
            {
                options.UseSqlite(
                    $"Data Source={_dbPath}",
                    b => b.MigrationsAssembly("EnterpriseDataManager.Data"));
            });

            // Remove all background IHostedService registrations so tests are fast and isolated
            services.RemoveAll<IHostedService>();

            // Override JWT validation parameters AFTER the app has registered its own JwtBearer
            // config — PostConfigure runs last and wins over any Configure call.
            // ClockSkew = Zero ensures tokens expiring in the past are rejected without the
            // default 5-minute grace period, which is required by the expiry-token security tests.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestIssuer,
                    ValidateAudience = true,
                    ValidAudience = TestAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret)),
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure schema is created after the host is built
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    /// <summary>
    /// Returns an <see cref="HttpClient"/> with a valid Bearer JWT pre-attached.
    /// </summary>
    public HttpClient GetAuthenticatedClient(string role = "Admin")
    {
        var token = GenerateJwtToken(role);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Seeds the database using the provided action executed within a scoped <see cref="EnterpriseDataManagerDbContext"/>.
    /// </summary>
    public async Task SeedAsync(Func<EnterpriseDataManagerDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        await seed(db);
    }

    /// <summary>
    /// Generates a signed JWT token for use in test HTTP requests.
    /// </summary>
    public string GenerateJwtToken(
        string role = "Admin",
        DateTime? expires = null,
        string? signingKey = null,
        string? issuer = null,
        string? audience = null)
    {
        var keyBytes = Encoding.UTF8.GetBytes(signingKey ?? TestJwtSecret);
        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "test-user-id"),
            new(JwtRegisteredClaimNames.Email, "test@example.com"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };

        var effectiveExpires = expires ?? DateTime.UtcNow.AddHours(1);
        var token = new JwtSecurityToken(
            issuer: issuer ?? TestIssuer,
            audience: audience ?? TestAudience,
            claims: claims,
            notBefore: effectiveExpires.AddHours(-1),
            expires: effectiveExpires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; temp files are cleaned up by the OS eventually
            }
        }
    }
}
