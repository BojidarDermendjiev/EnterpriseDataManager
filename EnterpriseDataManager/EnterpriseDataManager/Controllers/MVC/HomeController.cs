namespace EnterpriseDataManager.Controllers.MVC;

using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Data;
using EnterpriseDataManager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

public class HomeController : Controller
{
    private readonly EnterpriseDataManagerDbContext _db;
    private readonly ILogger<HomeController> _logger;

    public HomeController(EnterpriseDataManagerDbContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var totalJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(cancellationToken);
        var successfulJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Completed, cancellationToken);
        var failedJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Failed, cancellationToken);
        var pendingJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Draft || j.Status == ArchiveStatus.Scheduled, cancellationToken);
        var runningJobs = await _db.ArchiveJobs.AsNoTracking().CountAsync(j => j.Status == ArchiveStatus.Running, cancellationToken);
        var totalItems = await _db.ArchiveItems.AsNoTracking().CountAsync(cancellationToken);
        var totalSize = await _db.ArchiveJobs.AsNoTracking().SumAsync(j => j.ProcessedBytes, cancellationToken);
        var activePolicies = await _db.RetentionPolicies.AsNoTracking().CountAsync(cancellationToken);
        var activeProviders = await _db.StorageProviders.AsNoTracking().CountAsync(p => p.IsEnabled, cancellationToken);

        var recentJobs = await _db.ArchiveJobs
            .AsNoTracking()
            .Include(j => j.ArchivePlan)
            .OrderByDescending(j => j.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var storageByProvider = await _db.StorageProviders
            .AsNoTracking()
            .Select(p => new DashboardStorageRow
            {
                ProviderName = p.Name,
                ItemCount = p.ArchivePlans
                    .SelectMany(pl => pl.ArchiveJobs)
                    .SelectMany(j => j.Items)
                    .Count(),
                TotalSizeBytes = p.ArchivePlans
                    .SelectMany(pl => pl.ArchiveJobs)
                    .Sum(j => j.ProcessedBytes)
            })
            .ToListAsync(cancellationToken);

        var model = new DashboardViewModel
        {
            TotalArchiveJobs = totalJobs,
            SuccessfulJobs = successfulJobs,
            FailedJobs = failedJobs,
            PendingJobs = pendingJobs,
            RunningJobs = runningJobs,
            TotalArchivedItems = totalItems,
            TotalSizeBytes = totalSize,
            ActiveRetentionPolicies = activePolicies,
            ActiveStorageProviders = activeProviders,
            RecentJobs = recentJobs,
            StorageByProvider = storageByProvider
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class DashboardViewModel
{
    public int TotalArchiveJobs { get; set; }
    public int SuccessfulJobs { get; set; }
    public int FailedJobs { get; set; }
    public int PendingJobs { get; set; }
    public int RunningJobs { get; set; }
    public int TotalArchivedItems { get; set; }
    public long TotalSizeBytes { get; set; }
    public int ActiveRetentionPolicies { get; set; }
    public int ActiveStorageProviders { get; set; }
    public List<Core.Entities.ArchiveJob> RecentJobs { get; set; } = new();
    public List<DashboardStorageRow> StorageByProvider { get; set; } = new();
}

public class DashboardStorageRow
{
    public string ProviderName { get; set; } = "";
    public int ItemCount { get; set; }
    public long TotalSizeBytes { get; set; }
}
