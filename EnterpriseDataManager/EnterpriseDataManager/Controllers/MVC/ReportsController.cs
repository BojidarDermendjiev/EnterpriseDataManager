namespace EnterpriseDataManager.Controllers.MVC;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;

public sealed class ReportsController : Controller
{
    private readonly IArchivePlanRepository _archivePlanRepository;
    private readonly IArchiveJobRepository _archiveJobRepository;
    private readonly IRecoveryJobRepository _recoveryJobRepository;
    private readonly IAuditService _auditService;

    public ReportsController(
        IArchivePlanRepository archivePlanRepository,
        IArchiveJobRepository archiveJobRepository,
        IRecoveryJobRepository recoveryJobRepository,
        IAuditService auditService)
    {
        _archivePlanRepository = archivePlanRepository;
        _archiveJobRepository = archiveJobRepository;
        _recoveryJobRepository = recoveryJobRepository;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Reports";

        var archivePlans = await _archivePlanRepository.GetAllAsync(cancellationToken);
        var archiveJobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var recoveryJobs = await _recoveryJobRepository.GetAllAsync(cancellationToken);

        var model = new ReportsViewModel
        {
            TotalArchivePlans = archivePlans.Count,
            ActiveArchivePlans = archivePlans.Count(p => p.IsActive),
            TotalArchiveJobs = archiveJobs.Count,
            CompletedArchiveJobs = archiveJobs.Count(j => j.Status == Core.Enums.ArchiveStatus.Completed),
            FailedArchiveJobs = archiveJobs.Count(j => j.Status == Core.Enums.ArchiveStatus.Failed),
            RunningArchiveJobs = archiveJobs.Count(j => j.Status == Core.Enums.ArchiveStatus.Running),
            TotalRecoveryJobs = recoveryJobs.Count,
            CompletedRecoveryJobs = recoveryJobs.Count(j => j.Status == Core.Enums.ArchiveStatus.Completed),
            TotalDataArchived = archiveJobs.Sum(j => j.ProcessedBytes),
            RecentArchiveJobs = archiveJobs
                .OrderByDescending(j => j.CreatedAt)
                .Take(10)
                .ToList(),
            RecentRecoveryJobs = recoveryJobs
                .OrderByDescending(j => j.CreatedAt)
                .Take(10)
                .ToList()
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
}
