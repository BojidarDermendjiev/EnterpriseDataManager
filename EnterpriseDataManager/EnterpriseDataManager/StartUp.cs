namespace EnterpriseDataManager;

using EnterpriseDataManager.Application;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Data;
using EnterpriseDataManager.Filters;
using EnterpriseDataManager.Infrastructure;
using EnterpriseDataManager.Infrastructure.Time;
using EnterpriseDataManager.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;

public static class Startup
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configuration
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Database Context
        builder.Services.AddDbContext<EnterpriseDataManagerDbContext>(options =>
            options.UseSqlServer(connectionString));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // Identity
        builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<EnterpriseDataManagerDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            // Sign-in settings
            options.SignIn.RequireConfirmedAccount = false;

            // Enterprise-grade password policy
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;

            // Lockout settings (protection against brute force)
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
        });

        // Add layers using extension methods
        builder.Services.AddDataLayer(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructure(builder.Configuration);

        // Date/time provider (stateless, safe as singleton)
        builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Memory Cache for various services
        builder.Services.AddMemoryCache();

        // Register Filters
        builder.Services.AddScoped<AuditActionFilter>();

        // MVC Controllers and Views
        builder.Services.AddControllersWithViews(options =>
        {
            // Add global filters
            options.Filters.Add<ValidateModelAttribute>();
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // API Controllers
        builder.Services.AddEndpointsApiExplorer();

        // Swagger/OpenAPI
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Enterprise Data Manager API",
                Description = "API for managing data archival, recovery, and retention policies",
                Contact = new OpenApiContact
                {
                    Name = "Support",
                    Email = "support@enterprisedatamanager.com"
                }
            });

            // Add JWT authentication to Swagger
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // CORS - Secure configuration for all environments
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "https://localhost:5001", "https://localhost:7001" };

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Default", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
        });

        // Rate Limiting - Protection against abuse and brute force attacks
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global rate limit per user/IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Use authenticated user name, or IP address, or "anonymous"
                var partitionKey = context.User?.Identity?.Name
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var retryAfterSeconds = 60;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = (int)retryAfter.TotalSeconds;
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests. Please try again later.",
                    retryAfter = retryAfterSeconds
                }, cancellationToken);
            };
        });

        // Health Checks
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<EnterpriseDataManagerDbContext>();

        // Response Compression
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        var app = builder.Build();

        // Configure the HTTP request pipeline

        // Exception Handling (should be first)
        app.UseGlobalExceptionHandler();

        // Security Headers - Enterprise-grade configuration
        app.UseSecurityHeaders(options =>
        {
            options.EnableHsts = !app.Environment.IsDevelopment();
            options.HstsMaxAge = 31536000; // 1 year
            options.FrameOptionsPolicy = "DENY";
            options.EnableNoSniff = true;
            options.EnableXssProtection = true;
            options.RemoveServerHeader = true;
            options.ReferrerPolicy = "strict-origin-when-cross-origin";
            options.PermissionsPolicy = "geolocation=(), microphone=(), camera=(), usb=()";
            options.EnableNoCacheForAuthenticated = true;
            options.ContentSecurityPolicy = app.Environment.IsDevelopment()
                ? "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' https:;"
                : "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' https:; frame-ancestors 'none';";
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Enterprise Data Manager API v1");
                options.RoutePrefix = "api-docs";
            });
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseResponseCompression();

        app.UseRouting();

        // CORS - Use secure policy for all environments
        app.UseCors("Default");

        // Rate Limiting
        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        // Audit Logging (after authentication so we have user context)
        app.UseAuditLogging(options =>
        {
            options.EnableDatabaseLogging = true;
            options.ExcludedPaths = new[]
            {
                "/health",
                "/healthz",
                "/ready",
                "/api-docs",
                "/swagger",
                "/_framework",
                "/css",
                "/js",
                "/lib",
                "/images",
                "/favicon.ico"
            };
        });

        // Health Check Endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/healthz");

        // MVC Routes
        app.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        // API Routes
        app.MapControllers();

        // Razor Pages (Identity)
        app.MapRazorPages();

        app.Run();
    }
}
