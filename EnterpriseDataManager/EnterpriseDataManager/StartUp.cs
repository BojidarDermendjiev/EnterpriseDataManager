namespace EnterpriseDataManager;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

using EnterpriseDataManager.Data;
using EnterpriseDataManager.Application;
using EnterpriseDataManager.Infrastructure;
using EnterpriseDataManager.Middleware;
using EnterpriseDataManager.Filters;

public class StartUp
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Configuration
        string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
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
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequiredLength = 6;
        });

        // Add layers using extension methods
        builder.Services.AddDataLayer(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructure(builder.Configuration);

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

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });

            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins(
                    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
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

        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline

        // Exception Handling (should be first)
        app.UseGlobalExceptionHandler();

        // Security Headers
        app.UseSecurityHeaders(options =>
        {
            options.EnableHsts = !app.Environment.IsDevelopment();
            options.FrameOptionsPolicy = "SAMEORIGIN";
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

        // CORS
        app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "Production");

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
