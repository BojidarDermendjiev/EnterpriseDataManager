namespace EnterpriseDataManager.Controllers.MVC;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;

public sealed class ArchiveJobsController : Controller
{
    private readonly IArchiveJobRepository _archiveJobRepository;
    private readonly IArchivePlanRepository _archivePlanRepository;
    private readonly IArchiveService _archiveService;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveJobsController(
        IArchiveJobRepository archiveJobRepository,
        IArchivePlanRepository archivePlanRepository,
        IArchiveService archiveService,
        IUnitOfWork unitOfWork)
    {
        _archiveJobRepository = archiveJobRepository;
        _archivePlanRepository = archivePlanRepository;
        _archiveService = archiveService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(
        string? search = null,
        string? status = null,
        Guid? planId = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Archive Jobs";

        var jobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);

        // Apply filters
        if (!string.IsNullOrEmpty(search))
        {
            jobs = jobs.Where(j => j.ArchivePlan?.Name.Contains(search, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ArchiveStatus>(status, true, out var statusValue))
        {
            jobs = jobs.Where(j => j.Status == statusValue).ToList();
        }

        if (planId.HasValue)
        {
            jobs = jobs.Where(j => j.ArchivePlanId == planId.Value).ToList();
        }

        const int pageSize = 10;
        var totalCount = jobs.Count;
        var pagedJobs = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);

        var model = new ArchiveJobsViewModel
        {
            Jobs = pagedJobs,
            Plans = plans.ToList(),
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            Search = search,
            Status = status,
            PlanId = planId,
            RunningCount = jobs.Count(j => j.Status == ArchiveStatus.Running),
            ScheduledCount = jobs.Count(j => j.Status == ArchiveStatus.Scheduled),
            CompletedCount = jobs.Count(j => j.Status == ArchiveStatus.Completed && j.CompletedAt >= sevenDaysAgo),
            FailedCount = jobs.Count(j => j.Status == ArchiveStatus.Failed && j.CompletedAt >= sevenDaysAgo)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Archive Jobs / Details";

        var job = await _archiveJobRepository.GetByIdAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound();
        }

        return View(job);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Archive Jobs / New Job";

        var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);
        var activePlans = plans.Where(p => p.IsActive).ToList();

        var model = new CreateArchiveJobViewModel
        {
            Plans = activePlans
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateArchiveJobViewModel model, CancellationToken cancellationToken = default)
    {
        if (!model.PlanId.HasValue)
        {
            ModelState.AddModelError("PlanId", "Please select an archive plan.");
            var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);
            model.Plans = plans.Where(p => p.IsActive).ToList();
            return View(model);
        }

        try
        {
            await _archiveService.StartArchiveAsync(model.PlanId.Value, cancellationToken);
            TempData["Success"] = "Archive job started successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);
            model.Plans = plans.Where(p => p.IsActive).ToList();
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _archiveJobRepository.GetByIdAsync(id, cancellationToken);
            if (job == null)
            {
                TempData["Error"] = "Job not found.";
                return RedirectToAction(nameof(Index));
            }

            if (job.Status == Core.Enums.ArchiveStatus.Running)
            {
                TempData["Error"] = "Cannot delete a running job. Cancel it first.";
                return RedirectToAction(nameof(Index));
            }

            _archiveJobRepository.Remove(job);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Archive job deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid planId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _archiveService.StartArchiveAsync(planId, cancellationToken);
            TempData["Success"] = "Archive job started successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _archiveService.CancelArchiveAsync(id, cancellationToken);
            TempData["Success"] = "Archive job cancelled successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _archiveJobRepository.GetByIdAsync(id, cancellationToken);
            if (job == null)
            {
                TempData["Error"] = "Job not found.";
                return RedirectToAction(nameof(Index));
            }

            await _archiveService.StartArchiveAsync(job.ArchivePlanId, cancellationToken);
            TempData["Success"] = "Archive job restarted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetProgress(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _archiveJobRepository.GetByIdAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound();
        }

        var progress = job.TotalItemCount > 0
            ? (int)Math.Round((double)job.ProcessedItemCount / job.TotalItemCount * 100)
            : 0;

        return Json(new
        {
            id = job.Id,
            status = job.Status.ToString(),
            progress,
            processedItems = job.ProcessedItemCount,
            totalItems = job.TotalItemCount,
            processedBytes = job.ProcessedBytes,
            errorMessage = job.FailureReason
        });
    }
}

public class ArchiveJobsViewModel
{
    public List<ArchiveJob> Jobs { get; set; } = new();
    public List<ArchivePlan> Plans { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? Search { get; set; }
    public string? Status { get; set; }
    public Guid? PlanId { get; set; }
    public int RunningCount { get; set; }
    public int ScheduledCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
}

public class CreateArchiveJobViewModel
{
    public Guid? PlanId { get; set; }
    public List<ArchivePlan> Plans { get; set; } = new();
}
