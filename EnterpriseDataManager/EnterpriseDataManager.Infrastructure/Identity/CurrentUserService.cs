namespace EnterpriseDataManager.Infrastructure.Identity;

using System.Security.Claims;
using EnterpriseDataManager.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

    public string? UserId =>
        HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? HttpContext?.User?.FindFirst("sub")?.Value;

    public string? UserName =>
        HttpContext?.User?.Identity?.Name
        ?? HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public string? IpAddress
    {
        get
        {
            var ctx = HttpContext;
            if (ctx == null) return null;

            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && forwarded.Count > 0)
            {
                var first = forwarded.ToString().Split(',').Select(x => x.Trim()).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent => HttpContext?.Request?.Headers["User-Agent"].ToString();

    public bool IsAuthenticated => HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var claims = HttpContext?.User?.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            return claims?.AsReadOnly() ?? (IReadOnlyCollection<string>)Array.Empty<string>();
        }
    }

    public bool IsInRole(string role) => HttpContext?.User?.IsInRole(role) ?? false;
}
