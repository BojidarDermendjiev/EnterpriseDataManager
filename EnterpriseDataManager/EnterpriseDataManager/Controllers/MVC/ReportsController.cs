namespace EnterpriseDataManager.Controllers.MVC;

using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
public sealed class ReportsController : Controller
{
    private readonly EnterpriseDataManagerDbContext _db;

    public ReportsController(EnterpriseDataManagerDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Reports";

        var totalPlans = await _db.ArchivePlans.AsNoTracking().CountAsync(cancellationToken);
        var activePlans = await _db.ArchivePlans.AsNoTracking().CountAsync(p => p.IsActive, cancellationToken);
        var totalJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(cancellationToken);
        var completedJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Completed, cancellationToken);
        var failedJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Failed, cancellationToken);
        var runningJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Running, cancellationToken);
        var totalRecovery = await _db.RecoveryJobs.AsNoTracking().CountAsync(cancellationToken);
        var completedRecovery = await _db.RecoveryJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Completed, cancellationToken);
        var totalSize = await _db.ArchiveJobs.AsNoTracking().SumAsync(j => j.ProcessedBytes, cancellationToken);

        var recentArchiveJobs = await _db.ArchiveJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentRecoveryJobs = await _db.RecoveryJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var model = new ReportsViewModel
        {
            TotalArchivePlans = totalPlans,
            ActiveArchivePlans = activePlans,
            TotalArchiveJobs = totalJobs,
            CompletedArchiveJobs = completedJobs,
            FailedArchiveJobs = failedJobs,
            RunningArchiveJobs = runningJobs,
            TotalRecoveryJobs = totalRecovery,
            CompletedRecoveryJobs = completedRecovery,
            TotalDataArchived = totalSize,
            RecentArchiveJobs = recentArchiveJobs,
            RecentRecoveryJobs = recentRecoveryJobs
        };

        return View(model);
    }
}

public class ReportsViewModel
{
    public int TotalArchivePlans { get; set; }
    public int ActiveArchivePlans { get; set; }
    public int TotalArchiveJobs { get; set; }
    public int CompletedArchiveJobs { get; set; }
    public int FailedArchiveJobs { get; set; }
    public int RunningArchiveJobs { get; set; }
    public int TotalRecoveryJobs { get; set; }
    public int CompletedRecoveryJobs { get; set; }
    public long TotalDataArchived { get; set; }
    public List<Core.Entities.ArchiveJob> RecentArchiveJobs { get; set; } = new();
    public List<Core.Entities.RecoveryJob> RecentRecoveryJobs { get; set; } = new();

    public DateTimeOffset DefaultFrom => DateTimeOffset.UtcNow.AddYears(-1);
    public DateTimeOffset DefaultTo => DateTimeOffset.UtcNow;
}
