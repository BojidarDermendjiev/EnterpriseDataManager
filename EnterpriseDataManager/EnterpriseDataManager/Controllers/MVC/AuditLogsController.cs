namespace EnterpriseDataManager.Controllers.MVC;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
public sealed class AuditLogsController : Controller
{
    private readonly EnterpriseDataManagerDbContext _db;

    public AuditLogsController(EnterpriseDataManagerDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(
        string? actor = null,
        string? action = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Audit Logs";

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;

        var query = _db.AuditRecords.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(r => r.Actor.Contains(actor));

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(r => r.Action.Contains(action));

        if (from.HasValue)
            query = query.Where(r => r.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.Timestamp <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(r => r.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var model = new AuditLogViewModel
        {
            Records = records,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            ActorFilter = actor,
            ActionFilter = action,
            FromFilter = from,
            ToFilter = to
        };

        return View(model);
    }
}

public class AuditLogViewModel
{
    public List<AuditRecord> Records { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? ActorFilter { get; set; }
    public string? ActionFilter { get; set; }
    public DateTimeOffset? FromFilter { get; set; }
    public DateTimeOffset? ToFilter { get; set; }
}
