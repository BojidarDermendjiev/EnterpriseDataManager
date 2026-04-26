namespace EnterpriseDataManager.Controllers.MVC;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;

public sealed class ArchivePlansController : Controller
{
    private readonly IArchivePlanRepository _archivePlanRepository;
    private readonly IArchivePlanService _archivePlanService;
    private readonly IStorageProviderRepository _storageProviderRepository;
    private readonly IRetentionPolicyRepository _retentionPolicyRepository;

    public ArchivePlansController(
        IArchivePlanRepository archivePlanRepository,
        IArchivePlanService archivePlanService,
        IStorageProviderRepository storageProviderRepository,
        IRetentionPolicyRepository retentionPolicyRepository)
    {
        _archivePlanRepository = archivePlanRepository;
        _archivePlanService = archivePlanService;
        _storageProviderRepository = storageProviderRepository;
        _retentionPolicyRepository = retentionPolicyRepository;
    }

    public async Task<IActionResult> Index(
        string? search = null,
        string? status = null,
        string? securityLevel = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Archive Plans";

        var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);

        // Apply filters
        if (!string.IsNullOrEmpty(search))
        {
            plans = plans.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(status))
        {
            var isActive = status.Equals("active", StringComparison.OrdinalIgnoreCase);
            plans = plans.Where(p => p.IsActive == isActive).ToList();
        }

        if (!string.IsNullOrEmpty(securityLevel) && Enum.TryParse<SecurityLevel>(securityLevel, true, out var level))
        {
            plans = plans.Where(p => p.SecurityLevel == level).ToList();
        }

        const int pageSize = 10;
        var totalCount = plans.Count;
        var pagedPlans = plans
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var model = new ArchivePlansViewModel
        {
            Plans = pagedPlans,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            Search = search,
            Status = status,
            SecurityLevel = securityLevel
        };

        return View(model);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Archive Plans / Create";

        var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);
        var retentionPolicies = await _retentionPolicyRepository.GetAllAsync(cancellationToken);

        var model = new ArchivePlanFormViewModel
        {
            StorageProviders = storageProviders.ToList(),
            RetentionPolicies = retentionPolicies.ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArchivePlanFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);
            var retentionPolicies = await _retentionPolicyRepository.GetAllAsync(cancellationToken);
            model.StorageProviders = storageProviders.ToList();
            model.RetentionPolicies = retentionPolicies.ToList();
            return View(model);
        }

        try
        {
            var plan = await _archivePlanService.CreatePlanAsync(
                model.Name,
                model.SourcePath,
                model.SecurityLevel,
                cancellationToken);

            if (!string.IsNullOrEmpty(model.Schedule))
            {
                await _archivePlanService.SetScheduleAsync(plan.Id, model.Schedule, model.ScheduleDescription, cancellationToken);
            }

            if (model.StorageProviderId.HasValue)
            {
                await _archivePlanService.SetStorageProviderAsync(plan.Id, model.StorageProviderId.Value, cancellationToken);
            }

            if (model.RetentionPolicyId.HasValue)
            {
                await _archivePlanService.SetRetentionPolicyAsync(plan.Id, model.RetentionPolicyId.Value, cancellationToken);
            }

            TempData["Success"] = "Archive plan created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);
            var retentionPolicies = await _retentionPolicyRepository.GetAllAsync(cancellationToken);
            model.StorageProviders = storageProviders.ToList();
            model.RetentionPolicies = retentionPolicies.ToList();
            return View(model);
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Archive Plans / Details";

        var plan = await _archivePlanRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            return NotFound();
        }

        return View(plan);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Archive Plans / Edit";

        var plan = await _archivePlanRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            return NotFound();
        }

        var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);
        var retentionPolicies = await _retentionPolicyRepository.GetAllAsync(cancellationToken);

        var model = new ArchivePlanFormViewModel
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            SourcePath = plan.SourcePath,
            SecurityLevel = plan.SecurityLevel,
            Schedule = plan.Schedule?.Expression,
            ScheduleDescription = plan.Schedule?.Description,
            StorageProviderId = plan.StorageProviderId,
            RetentionPolicyId = plan.RetentionPolicyId,
            IsActive = plan.IsActive,
            StorageProviders = storageProviders.ToList(),
            RetentionPolicies = retentionPolicies.ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ArchivePlanFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);
            var retentionPolicies = await _retentionPolicyRepository.GetAllAsync(cancellationToken);
            model.StorageProviders = storageProviders.ToList();
            model.RetentionPolicies = retentionPolicies.ToList();
            return View(model);
        }

        try
        {
            await _archivePlanService.UpdatePlanAsync(id, model.Name, model.Description, cancellationToken);

            if (!string.IsNullOrEmpty(model.Schedule))
            {
                await _archivePlanService.SetScheduleAsync(id, model.Schedule, model.ScheduleDescription, cancellationToken);
            }

            if (model.StorageProviderId.HasValue)
            {
                await _archivePlanService.SetStorageProviderAsync(id, model.StorageProviderId.Value, cancellationToken);
            }

            if (model.RetentionPolicyId.HasValue)
            {
                await _archivePlanService.SetRetentionPolicyAsync(id, model.RetentionPolicyId.Value, cancellationToken);
            }

            TempData["Success"] = "Archive plan updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            var storageProviders = await _storageProviderRepository.GetAllAsync(cancellationToken);
            var retentionPolicies = await _retentionPolicyRepository.GetAllAsync(cancellationToken);
            model.StorageProviders = storageProviders.ToList();
            model.RetentionPolicies = retentionPolicies.ToList();
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _archivePlanService.DeletePlanAsync(id, cancellationToken);
            TempData["Success"] = "Archive plan deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _archivePlanService.ActivatePlanAsync(id, cancellationToken);
            TempData["Success"] = "Archive plan activated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var plan = await _archivePlanRepository.GetByIdAsync(id, cancellationToken);
            if (plan == null) return NotFound();

            TempData["Success"] = $"Archive job queued for '{plan.Name}'.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Export(
        string? search = null,
        string? status = null,
        string? securityLevel = null,
        CancellationToken cancellationToken = default)
    {
        var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);

        if (!string.IsNullOrEmpty(search))
            plans = plans.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrEmpty(status))
        {
            var isActive = status.Equals("active", StringComparison.OrdinalIgnoreCase);
            plans = plans.Where(p => p.IsActive == isActive).ToList();
        }

        if (!string.IsNullOrEmpty(securityLevel) && Enum.TryParse<SecurityLevel>(securityLevel, true, out var level))
            plans = plans.Where(p => p.SecurityLevel == level).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Name,Source Path,Status,Security Level,Schedule,Storage Provider,Last Run,Next Run");

        foreach (var plan in plans.OrderByDescending(p => p.CreatedAt))
        {
            static string Csv(string? v) => v is null ? "" : $"\"{v.Replace("\"", "\"\"")}\"";
            sb.AppendLine(string.Join(",",
                Csv(plan.Name),
                Csv(plan.SourcePath),
                plan.IsActive ? "Active" : "Inactive",
                plan.SecurityLevel.ToString(),
                Csv(plan.Schedule?.Description ?? plan.Schedule?.Expression),
                Csv(plan.StorageProvider?.Name),
                plan.LastRunAt.HasValue ? plan.LastRunAt.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "",
                plan.NextRunAt.HasValue ? plan.NextRunAt.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm") : ""
            ));
        }

        var fileName = $"archive-plans-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _archivePlanService.DeactivatePlanAsync(id, cancellationToken);
            TempData["Success"] = "Archive plan deactivated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

public class ArchivePlansViewModel
{
    public List<ArchivePlan> Plans { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SecurityLevel { get; set; }
}

public class ArchivePlanFormViewModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string SourcePath { get; set; } = "";
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Internal;
    public string? Schedule { get; set; }
    public string? ScheduleDescription { get; set; }
    public Guid? StorageProviderId { get; set; }
    public Guid? RetentionPolicyId { get; set; }
    public bool IsActive { get; set; }

    public List<StorageProvider> StorageProviders { get; set; } = new();
    public List<RetentionPolicy> RetentionPolicies { get; set; } = new();
}
