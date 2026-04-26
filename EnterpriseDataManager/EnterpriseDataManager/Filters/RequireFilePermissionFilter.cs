namespace EnterpriseDataManager.Filters;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

/// <summary>
/// Enforces file-level ACL. Apply to actions that serve or mutate a specific ArchiveItem.
/// Resolves archiveItemId from the route parameter named "archiveItemId".
/// Users with the "Admin" role bypass the check.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequireFilePermissionAttribute : Attribute, IFilterFactory
{
    public PermissionLevel MinimumLevel { get; }
    public bool IsReusable => false;

    public RequireFilePermissionAttribute(PermissionLevel minimumLevel = PermissionLevel.Read)
    {
        MinimumLevel = minimumLevel;
    }

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        => new FilePermissionFilter(
            serviceProvider.GetRequiredService<EnterpriseDataManagerDbContext>(),
            MinimumLevel);
}

internal sealed class FilePermissionFilter : IAsyncActionFilter
{
    private readonly EnterpriseDataManagerDbContext _db;
    private readonly PermissionLevel _minimumLevel;

    public FilePermissionFilter(EnterpriseDataManagerDbContext db, PermissionLevel minimumLevel)
    {
        _db = db;
        _minimumLevel = minimumLevel;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.RouteData.Values.TryGetValue("archiveItemId", out var itemIdObj) || itemIdObj is null)
        {
            await next();
            return;
        }

        var archiveItemId = itemIdObj.ToString()!;
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (context.HttpContext.User.IsInRole("Admin"))
        {
            await next();
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var permission = await _db.FilePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.ArchiveItemId == archiveItemId &&
                p.UserId == userId &&
                (p.ExpiresAt == null || p.ExpiresAt > now));

        if (permission == null || (int)permission.Level < (int)_minimumLevel)
        {
            context.Result = new ObjectResult(new
            {
                error = "You do not have sufficient permission to access this file.",
                code = "FORBIDDEN",
                required = _minimumLevel.ToString()
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
