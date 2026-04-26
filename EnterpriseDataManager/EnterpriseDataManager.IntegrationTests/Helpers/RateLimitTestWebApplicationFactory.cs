namespace EnterpriseDataManager.IntegrationTests.Helpers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.RateLimiting;

/// <summary>
/// A test factory that lowers rate-limit thresholds so tests can exhaust them
/// without sending hundreds of requests.
/// </summary>
public class RateLimitTestWebApplicationFactory : TestWebApplicationFactory
{
    private const int SmallGlobalLimit = 5;
    private const int SmallApiLimit = 5;
    private const int SmallAuthLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Apply base configuration (Sqlite, JWT secret, disable background jobs, etc.)
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Remove the production rate limiter registration and replace with test-friendly limits
            var rateLimiterDescriptors = services
                .Where(d => d.ServiceType.FullName != null &&
                            d.ServiceType.FullName.Contains("RateLimiter", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var descriptor in rateLimiterDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddFixedWindowLimiter("api", limiterOptions =>
                {
                    limiterOptions.PermitLimit = SmallApiLimit;
                    limiterOptions.Window = TimeSpan.FromSeconds(60);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 0;
                });

                options.AddFixedWindowLimiter("auth", limiterOptions =>
                {
                    limiterOptions.PermitLimit = SmallAuthLimit;
                    limiterOptions.Window = TimeSpan.FromSeconds(60);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 0;
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
                            PermitLimit = SmallGlobalLimit,
                            Window = TimeSpan.FromSeconds(60),
                            QueueLimit = 0
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
        });
    }
}
