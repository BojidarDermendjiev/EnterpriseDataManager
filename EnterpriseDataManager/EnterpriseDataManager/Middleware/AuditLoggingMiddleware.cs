namespace EnterpriseDataManager.Middleware;

using EnterpriseDataManager.Core.Interfaces.Services;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Middleware that logs HTTP request/response information for audit purposes.
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly AuditLoggingOptions _options;

    public AuditLoggingMiddleware(
        RequestDelegate next,
        ILogger<AuditLoggingMiddleware> logger,
        AuditLoggingOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new AuditLoggingOptions();
    }

    public async Task InvokeAsync(HttpContext context, IAuditService? auditService = null)
    {
        // Skip excluded paths
        if (ShouldSkipPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var correlationId = GetOrCreateCorrelationId(context);
        var requestInfo = CaptureRequestInfo(context);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var responseInfo = CaptureResponseInfo(context, stopwatch.Elapsed);

            // Log the request/response
            await LogAuditRecordAsync(context, auditService, requestInfo, responseInfo, correlationId);
        }
    }

    private bool ShouldSkipPath(PathString path)
    {
        if (_options.ExcludedPaths == null || !_options.ExcludedPaths.Any())
            return false;

        return _options.ExcludedPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        const string correlationIdHeader = "X-Correlation-Id";

        if (context.Request.Headers.TryGetValue(correlationIdHeader, out var existingId) && !string.IsNullOrEmpty(existingId))
        {
            context.Response.Headers[correlationIdHeader] = existingId;
            return existingId!;
        }

        var newId = Guid.NewGuid().ToString("N");
        context.Response.Headers[correlationIdHeader] = newId;
        return newId;
    }

    private RequestInfo CaptureRequestInfo(HttpContext context)
    {
        var request = context.Request;

        return new RequestInfo
        {
            Method = request.Method,
            Path = request.Path.Value ?? "",
            QueryString = request.QueryString.HasValue ? request.QueryString.Value : null,
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            IpAddress = GetClientIpAddress(context),
            UserAgent = request.Headers.UserAgent.FirstOrDefault(),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static ResponseInfo CaptureResponseInfo(HttpContext context, TimeSpan duration)
    {
        return new ResponseInfo
        {
            StatusCode = context.Response.StatusCode,
            ContentType = context.Response.ContentType,
            Duration = duration
        };
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for X-Forwarded-For header (behind load balancer/proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        // Check for X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task LogAuditRecordAsync(
        HttpContext context,
        IAuditService? auditService,
        RequestInfo requestInfo,
        ResponseInfo responseInfo,
        string correlationId)
    {
        var actor = GetActor(context);
        var action = $"HTTP {requestInfo.Method} {requestInfo.Path}";
        var success = responseInfo.StatusCode >= 200 && responseInfo.StatusCode < 400;

        var details = new
        {
            CorrelationId = correlationId,
            Request = new
            {
                requestInfo.Method,
                requestInfo.Path,
                requestInfo.QueryString,
                requestInfo.ContentType,
                requestInfo.ContentLength,
                requestInfo.UserAgent
            },
            Response = new
            {
                responseInfo.StatusCode,
                responseInfo.ContentType,
                DurationMs = responseInfo.Duration.TotalMilliseconds
            },
            Client = new
            {
                IpAddress = requestInfo.IpAddress
            }
        };

        var detailsJson = JsonSerializer.Serialize(details);

        // Log to structured logging
        _logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {DurationMs:F1}ms - User: {Actor} - IP: {IpAddress} - CorrelationId: {CorrelationId}",
            requestInfo.Method,
            requestInfo.Path,
            responseInfo.StatusCode,
            responseInfo.Duration.TotalMilliseconds,
            actor,
            requestInfo.IpAddress,
            correlationId);

        // Log to audit service if available
        if (auditService != null && _options.EnableDatabaseLogging)
        {
            try
            {
                await auditService.LogSecurityEventAsync(
                    actor,
                    action,
                    detailsJson,
                    requestInfo.IpAddress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write audit record to database");
            }
        }
    }

    private static string GetActor(HttpContext context)
    {
        var user = context.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            return user.FindFirstValue(ClaimTypes.Name)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "authenticated-user";
        }

        return "anonymous";
    }

    private record RequestInfo
    {
        public string Method { get; init; } = "";
        public string Path { get; init; } = "";
        public string? QueryString { get; init; }
        public string? ContentType { get; init; }
        public long? ContentLength { get; init; }
        public string IpAddress { get; init; } = "";
        public string? UserAgent { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }

    private record ResponseInfo
    {
        public int StatusCode { get; init; }
        public string? ContentType { get; init; }
        public TimeSpan Duration { get; init; }
    }
}

/// <summary>
/// Options for configuring audit logging middleware.
/// </summary>
public class AuditLoggingOptions
{
    public bool EnableDatabaseLogging { get; set; } = true;
    public string[] ExcludedPaths { get; set; } = new[]
    {
        "/health",
        "/healthz",
        "/ready",
        "/metrics",
        "/_framework",
        "/css",
        "/js",
        "/lib",
        "/images",
        "/favicon.ico"
    };
    public bool LogRequestHeaders { get; set; } = false;
    public bool LogResponseHeaders { get; set; } = false;
    public bool LogRequestBody { get; set; } = false;
    public bool LogResponseBody { get; set; } = false;
    public int MaxBodyLogSize { get; set; } = 4096;
}

/// <summary>
/// Extension methods for adding audit logging middleware.
/// </summary>
public static class AuditLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditLoggingMiddleware>();
    }

    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder, AuditLoggingOptions options)
    {
        return builder.UseMiddleware<AuditLoggingMiddleware>(options);
    }

    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder, Action<AuditLoggingOptions> configure)
    {
        var options = new AuditLoggingOptions();
        configure(options);
        return builder.UseMiddleware<AuditLoggingMiddleware>(options);
    }
}
