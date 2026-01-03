namespace EnterpriseDataManager.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(RequestDelegate next, SecurityHeadersOptions? options = null)
    {
        _next = next;
        _options = options ?? new SecurityHeadersOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before the response is sent
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Prevent MIME type sniffing
            if (_options.EnableNoSniff)
            {
                headers["X-Content-Type-Options"] = "nosniff";
            }

            // Prevent clickjacking
            if (_options.EnableFrameOptions)
            {
                headers["X-Frame-Options"] = _options.FrameOptionsPolicy;
            }

            // XSS protection (legacy browsers)
            if (_options.EnableXssProtection)
            {
                headers["X-XSS-Protection"] = "1; mode=block";
            }

            // Referrer policy
            if (!string.IsNullOrEmpty(_options.ReferrerPolicy))
            {
                headers["Referrer-Policy"] = _options.ReferrerPolicy;
            }

            // Content Security Policy
            if (!string.IsNullOrEmpty(_options.ContentSecurityPolicy))
            {
                headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;
            }

            // Permissions Policy (formerly Feature-Policy)
            if (!string.IsNullOrEmpty(_options.PermissionsPolicy))
            {
                headers["Permissions-Policy"] = _options.PermissionsPolicy;
            }

            // Strict Transport Security (HTTPS only)
            if (_options.EnableHsts && context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = $"max-age={_options.HstsMaxAge}; includeSubDomains";
            }

            // Remove server header to prevent information disclosure
            if (_options.RemoveServerHeader)
            {
                headers.Remove("Server");
                headers.Remove("X-Powered-By");
            }

            // Cache control for sensitive pages
            if (_options.EnableNoCacheForAuthenticated && context.User?.Identity?.IsAuthenticated == true)
            {
                headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
                headers["Pragma"] = "no-cache";
                headers["Expires"] = "0";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Options for configuring security headers.
/// </summary>
public class SecurityHeadersOptions
{
    public bool EnableNoSniff { get; set; } = true;
    public bool EnableFrameOptions { get; set; } = true;
    public string FrameOptionsPolicy { get; set; } = "DENY";
    public bool EnableXssProtection { get; set; } = true;
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
    public string? ContentSecurityPolicy { get; set; } = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' https:;";
    public string? PermissionsPolicy { get; set; } = "geolocation=(), microphone=(), camera=()";
    public bool EnableHsts { get; set; } = true;
    public int HstsMaxAge { get; set; } = 31536000; // 1 year
    public bool RemoveServerHeader { get; set; } = true;
    public bool EnableNoCacheForAuthenticated { get; set; } = true;
}

/// <summary>
/// Extension methods for adding security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder, SecurityHeadersOptions options)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>(options);
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder, Action<SecurityHeadersOptions> configure)
    {
        var options = new SecurityHeadersOptions();
        configure(options);
        return builder.UseMiddleware<SecurityHeadersMiddleware>(options);
    }
}
