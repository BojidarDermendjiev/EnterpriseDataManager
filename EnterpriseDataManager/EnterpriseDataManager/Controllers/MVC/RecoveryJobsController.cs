namespace EnterpriseDataManager.Controllers.MVC;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;

public sealed class RecoveryJobsController : Controller
{
    private readonly IRecoveryJobRepository _recoveryJobRepository;
    private readonly IArchiveJobRepository _archiveJobRepository;
    private readonly IRecoveryService _recoveryService;

    public RecoveryJobsController(
        IRecoveryJobRepository recoveryJobRepository,
        IArchiveJobRepository archiveJobRepository,
        IRecoveryService recoveryService)
    {
        _recoveryJobRepository = recoveryJobRepository;
        _archiveJobRepository = archiveJobRepository;
        _recoveryService = recoveryService;
    }

    public async Task<IActionResult> Index(
        string? search = null,
        string? status = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Recovery Jobs";

        var jobs = await _recoveryJobRepository.GetAllAsync(cancellationToken);

        // Apply filters
        if (!string.IsNullOrEmpty(search))
        {
            jobs = jobs.Where(j =>
                j.DestinationPath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                j.ArchiveJob?.ArchivePlan?.Name.Contains(search, StringComparison.OrdinalIgnoreCase) == true
            ).ToList();
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ArchiveStatus>(status, true, out var statusValue))
        {
            jobs = jobs.Where(j => j.Status == statusValue).ToList();
        }

        const int pageSize = 10;
        var totalCount = jobs.Count;
        var pagedJobs = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var model = new RecoveryJobsViewModel
        {
            Jobs = pagedJobs,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            Search = search,
            Status = status,
            InProgressCount = jobs.Count(j => j.Status == ArchiveStatus.Running),
            CompletedCount = jobs.Count(j => j.Status == ArchiveStatus.Completed && j.CompletedAt >= thirtyDaysAgo),
            FailedCount = jobs.Count(j => j.Status == ArchiveStatus.Failed),
            TotalDataRecovered = jobs.Where(j => j.Status == ArchiveStatus.Completed && j.CompletedAt >= thirtyDaysAgo).Sum(j => j.RecoveredBytes)
        };

        return View(model);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Recovery Jobs / New Recovery";

        var archiveJobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
        var completedJobs = archiveJobs
            .Where(j => j.Status == ArchiveStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .ToList();

        var model = new RecoveryJobFormViewModel
        {
            AvailableArchiveJobs = completedJobs
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecoveryJobFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var archiveJobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
            model.AvailableArchiveJobs = archiveJobs
                .Where(j => j.Status == ArchiveStatus.Completed)
                .OrderByDescending(j => j.CompletedAt)
                .ToList();
            return View(model);
        }

        try
        {
            var recoveryJob = await _recoveryService.CreateRecoveryJobAsync(model.ArchiveJobId, model.DestinationPath, cancellationToken);
            await _recoveryService.StartRecoveryAsync(recoveryJob.Id, cancellationToken);
            TempData["Success"] = "Recovery job started successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            var archiveJobs = await _archiveJobRepository.GetAllAsync(cancellationToken);
            model.AvailableArchiveJobs = archiveJobs
                .Where(j => j.Status == ArchiveStatus.Completed)
                .OrderByDescending(j => j.CompletedAt)
                .ToList();
            return View(model);
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Recovery Jobs / Details";

        var job = await _recoveryJobRepository.GetByIdAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound();
        }

        return View(job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _recoveryService.CancelRecoveryAsync(id, cancellationToken);
            TempData["Success"] = "Recovery job cancelled successfully.";
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
            var job = await _recoveryJobRepository.GetByIdAsync(id, cancellationToken);
            if (job == null)
            {
                TempData["Error"] = "Job not found.";
                return RedirectToAction(nameof(Index));
            }

            var newRecoveryJob = await _recoveryService.CreateRecoveryJobAsync(job.ArchiveJobId, job.DestinationPath, cancellationToken);
            await _recoveryService.StartRecoveryAsync(newRecoveryJob.Id, cancellationToken);
            TempData["Success"] = "Recovery job restarted successfully.";
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
        var job = await _recoveryJobRepository.GetByIdAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFound();
        }

        var progress = job.TotalItems > 0
            ? (int)Math.Round((double)job.RecoveredItems / job.TotalItems * 100)
            : 0;

        return Json(new
        {
            id = job.Id,
            status = job.Status.ToString(),
            progress,
            processedItems = job.RecoveredItems,
            totalItems = job.TotalItems,
            processedBytes = job.RecoveredBytes,
            errorMessage = job.FailureReason
        });
    }
}

public class RecoveryJobsViewModel
{
    public List<RecoveryJob> Jobs { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public long TotalDataRecovered { get; set; }
}

public class RecoveryJobFormViewModel
{
    public Guid ArchiveJobId { get; set; }
    public string DestinationPath { get; set; } = "";
    public List<ArchiveJob> AvailableArchiveJobs { get; set; } = new();
}
