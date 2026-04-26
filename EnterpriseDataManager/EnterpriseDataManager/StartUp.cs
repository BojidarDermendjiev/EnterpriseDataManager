namespace EnterpriseDataManager;

using System.Text;
using Asp.Versioning;
using EnterpriseDataManager.Application;
using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Data;
using EnterpriseDataManager.Filters;
using EnterpriseDataManager.Infrastructure;
using EnterpriseDataManager.Infrastructure.Identity;
using EnterpriseDataManager.Infrastructure.Time;
using EnterpriseDataManager.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Globalization;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;

public static class Startup
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Database Context — provider selectable via config for multi-environment support
        var provider = builder.Configuration.GetValue<string>("Database:Provider") ?? "SqlServer";
        builder.Services.AddDbContext<EnterpriseDataManagerDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            _ = provider switch
            {
                "PostgreSQL" => options.UseNpgsql(connectionString, b => b.MigrationsAssembly("EnterpriseDataManager.Data")),
                "Sqlite" => options.UseSqlite(connectionString, b => b.MigrationsAssembly("EnterpriseDataManager.Data")),
                _ => options.UseSqlServer(connectionString, b => b.MigrationsAssembly("EnterpriseDataManager.Data"))
            };
        });

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // ASP.NET Core Identity — cookie auth for MVC pages
        builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<EnterpriseDataManagerDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = true;
        });

        // JWT Bearer — additional scheme for API endpoints; cookie auth for MVC remains the default
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var jwtSecret = jwtOptions.Secret;

        if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException("Jwt:Secret is not configured for production. Set the Jwt__Secret environment variable.");

        if (!builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(jwtSecret))
        {
            var startupLogger = LoggerFactory.Create(lb => lb.AddConsole()).CreateLogger("Startup");
            startupLogger.LogWarning("Jwt:Secret is empty. Set Jwt__Secret in environment or appsettings before deploying.");
        }

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Identity.Application";
            options.DefaultChallengeScheme = "Identity.Application";
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jwtSecret) ? "placeholder-key-not-for-production-use" : jwtSecret))
            };
        });

        // Add layers using extension methods
        builder.Services.AddDataLayer(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        builder.Services.AddMemoryCache();

        // Localization
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        var supportedCultures = new[]
        {
            new CultureInfo("en"),
            new CultureInfo("bg")
        };
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("en");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.RequestCultureProviders = new List<IRequestCultureProvider>
            {
                new CookieRequestCultureProvider { CookieName = ".AspNetCore.Culture" },
                new QueryStringRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            };
        });

        // Cookie Policy
        builder.Services.Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = context => true;
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.ConsentCookieValue = "true";
        });

        builder.Services.AddScoped<AuditActionFilter>();

        // MVC Controllers and Views
        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add<ValidateModelAttribute>();
        })
        .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
        .AddDataAnnotationsLocalization()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        builder.Services.AddEndpointsApiExplorer();

        // API Versioning
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"));
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

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

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
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

        // CORS
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

        // Rate Limiting
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("api", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 10;
            });

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
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

        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromSeconds(31536000);
            options.IncludeSubDomains = true;
            options.Preload = true;
        });

        // Logging
        builder.Host.UseSerilog((ctx, lc) =>
        {
            lc.ReadFrom.Configuration(ctx.Configuration)
              .Enrich.FromLogContext()
              .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
              .WriteTo.File(
                  path: "logs/edm-.log",
                  rollingInterval: Serilog.RollingInterval.Day,
                  retainedFileCountLimit: 30,
                  outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

            var seqUrl = ctx.Configuration["Seq:ServerUrl"];
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                var apiKey = ctx.Configuration["Seq:ApiKey"];
                lc.WriteTo.Seq(seqUrl, apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
            }
        });

        var app = builder.Build();

        if (app.Environment.IsProduction())
        {
            var appLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            if (allowedOrigins.Any(o => o.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
            {
                appLogger.LogCritical(
                    "CORS configuration contains localhost origins in production: {Origins}. Remove localhost from Cors:AllowedOrigins.",
                    string.Join(", ", allowedOrigins.Where(o => o.Contains("localhost", StringComparison.OrdinalIgnoreCase))));
            }
        }

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Count > 0)
            {
                var startupLogger = scope.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Startup");
                startupLogger.LogCritical(
                    "{Count} pending database migration(s) detected at startup: {Migrations}. Run 'dotnet ef database update' or the migrate container before serving traffic.",
                    pending.Count,
                    string.Join(", ", pending));

                if (app.Environment.IsProduction())
                    throw new InvalidOperationException(
                        $"Cannot start in Production with {pending.Count} pending migration(s): {string.Join(", ", pending)}");
            }
        }

        app.UseGlobalExceptionHandler();

        app.UseSecurityHeaders(options =>
        {
            options.EnableHsts = !app.Environment.IsDevelopment();
            options.HstsMaxAge = 31536000;
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

        var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
        app.UseRequestLocalization(localizationOptions);

        app.UseCookiePolicy();
        app.UseRouting();
        app.UseCors("Default");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

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

        app.MapHealthChecks("/health");
        app.MapHealthChecks("/healthz");

        app.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapControllers();
        app.MapRazorPages();

        app.Run();
    }
}
