namespace EnterpriseDataManager.Controllers.MVC;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;

public sealed class StorageProvidersController : Controller
{
    private readonly IStorageProviderRepository _storageProviderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StorageProvidersController(
        IStorageProviderRepository storageProviderRepository,
        IUnitOfWork unitOfWork)
    {
        _storageProviderRepository = storageProviderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index(
        string? search = null,
        string? type = null,
        string? status = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Storage Providers";

        var providers = await _storageProviderRepository.GetAllAsync(cancellationToken);

        // Apply filters
        if (!string.IsNullOrEmpty(search))
        {
            providers = providers.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<StorageType>(type, true, out var typeValue))
        {
            providers = providers.Where(p => p.Type == typeValue).ToList();
        }

        if (!string.IsNullOrEmpty(status))
        {
            var isEnabled = status.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            providers = providers.Where(p => p.IsEnabled == isEnabled).ToList();
        }

        const int pageSize = 10;
        var totalCount = providers.Count;
        var pagedProviders = providers
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var model = new StorageProvidersViewModel
        {
            Providers = pagedProviders,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            Search = search,
            Type = type,
            Status = status,
            EnabledCount = providers.Count(p => p.IsEnabled),
            LocalCount = providers.Count(p => p.Type == StorageType.Local),
            S3Count = providers.Count(p => p.Type == StorageType.S3Compatible),
            AzureCount = providers.Count(p => p.Type == StorageType.AzureBlob)
        };

        return View(model);
    }

    public IActionResult Create()
    {
        ViewData["Breadcrumb"] = "Storage Providers / Add New";
        return View(new StorageProviderFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StorageProviderFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            StorageProvider provider;

            switch (model.Type)
            {
                case StorageType.Local:
                    provider = StorageProvider.CreateLocal(model.Name, model.RootPath ?? "");
                    break;
                case StorageType.S3Compatible:
                    provider = StorageProvider.CreateS3(model.Name, model.Endpoint ?? "", model.BucketOrContainer ?? "", model.CredentialsReference);
                    break;
                case StorageType.AzureBlob:
                    provider = StorageProvider.CreateAzureBlob(model.Name, model.Endpoint ?? "", model.BucketOrContainer ?? "", model.CredentialsReference);
                    break;
                default:
                    provider = StorageProvider.Create(model.Name, model.Type);
                    break;
            }

            if (!string.IsNullOrEmpty(model.Description))
            {
                provider.UpdateDetails(model.Name, model.Description);
            }

            if (model.QuotaGB.HasValue)
            {
                provider.SetQuota(model.QuotaGB.Value * 1024L * 1024L * 1024L);
            }

            if (model.IsImmutable)
            {
                provider.MakeImmutable();
            }

            if (!model.IsEnabled)
            {
                provider.Disable();
            }

            await _storageProviderRepository.AddAsync(provider, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Storage provider created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Storage Providers / Details";

        var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFound();
        }

        return View(provider);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken = default)
    {
        ViewData["Breadcrumb"] = "Storage Providers / Edit";

        var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFound();
        }

        var model = new StorageProviderFormViewModel
        {
            Id = provider.Id,
            Name = provider.Name,
            Description = provider.Description,
            Type = provider.Type,
            Endpoint = provider.Endpoint,
            BucketOrContainer = provider.BucketOrContainer,
            RootPath = provider.RootPath,
            CredentialsReference = provider.CredentialsReference,
            IsEnabled = provider.IsEnabled,
            IsImmutable = provider.IsImmutable,
            QuotaGB = provider.QuotaBytes.HasValue ? (int)(provider.QuotaBytes.Value / (1024L * 1024L * 1024L)) : null
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StorageProviderFormViewModel model, CancellationToken cancellationToken = default)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
            if (provider == null)
            {
                return NotFound();
            }

            provider.UpdateDetails(model.Name, model.Description);

            if (provider.Type != StorageType.Local)
            {
                if (!string.IsNullOrEmpty(model.Endpoint))
                {
                    provider.SetEndpoint(model.Endpoint);
                }
                if (!string.IsNullOrEmpty(model.BucketOrContainer))
                {
                    provider.SetBucketOrContainer(model.BucketOrContainer);
                }
            }

            provider.SetRootPath(model.RootPath);
            provider.SetCredentialsReference(model.CredentialsReference);

            if (model.QuotaGB.HasValue)
            {
                provider.SetQuota(model.QuotaGB.Value * 1024L * 1024L * 1024L);
            }
            else
            {
                provider.SetQuota(null);
            }

            if (model.IsEnabled)
            {
                provider.Enable();
            }
            else
            {
                provider.Disable();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Storage provider updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
            if (provider == null)
            {
                TempData["Error"] = "Storage provider not found.";
                return RedirectToAction(nameof(Index));
            }

            _storageProviderRepository.Remove(provider);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Storage provider deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
            if (provider == null)
            {
                TempData["Error"] = "Storage provider not found.";
                return RedirectToAction(nameof(Index));
            }

            provider.Enable();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Storage provider enabled successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
            if (provider == null)
            {
                TempData["Error"] = "Storage provider not found.";
                return RedirectToAction(nameof(Index));
            }

            provider.Disable();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Storage provider disabled successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
            if (provider == null)
            {
                return Json(new { success = false, message = "Storage provider not found." });
            }

            // Test connection based on provider type
            var (success, message) = await TestProviderConnectionAsync(provider, cancellationToken);

            return Json(new { success, message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> TestAllConnections(CancellationToken cancellationToken = default)
    {
        var providers = await _storageProviderRepository.GetEnabledProvidersAsync(cancellationToken);
        var results = new List<object>();

        foreach (var provider in providers)
        {
            try
            {
                var (success, message) = await TestProviderConnectionAsync(provider, cancellationToken);
                results.Add(new
                {
                    id = provider.Id,
                    name = provider.Name,
                    type = provider.Type.ToString(),
                    success,
                    message
                });
            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    id = provider.Id,
                    name = provider.Name,
                    type = provider.Type.ToString(),
                    success = false,
                    message = ex.Message
                });
            }
        }

        return Json(new { results });
    }

    private async Task<(bool success, string message)> TestProviderConnectionAsync(StorageProvider provider, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async operations

        switch (provider.Type)
        {
            case StorageType.Local:
                if (string.IsNullOrEmpty(provider.RootPath))
                {
                    return (false, "Root path is not configured.");
                }
                if (!Directory.Exists(provider.RootPath))
                {
                    try
                    {
                        Directory.CreateDirectory(provider.RootPath);
                        return (true, "Connection successful. Directory created.");
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Cannot access path: {ex.Message}");
                    }
                }
                return (true, "Connection successful. Directory exists and is accessible.");

            case StorageType.S3Compatible:
                if (string.IsNullOrEmpty(provider.Endpoint) || string.IsNullOrEmpty(provider.BucketOrContainer))
                {
                    return (false, "Endpoint and bucket must be configured.");
                }
                // In production, you would actually test the S3 connection here
                return (true, "S3 configuration validated. Full connectivity test requires credentials.");

            case StorageType.AzureBlob:
                if (string.IsNullOrEmpty(provider.Endpoint) || string.IsNullOrEmpty(provider.BucketOrContainer))
                {
                    return (false, "Endpoint and container must be configured.");
                }
                // In production, you would actually test the Azure Blob connection here
                return (true, "Azure Blob configuration validated. Full connectivity test requires credentials.");

            case StorageType.Tape:
                return (false, "Tape device connectivity testing not implemented.");

            default:
                return (false, "Unknown storage type.");
        }
    }
}

public class StorageProvidersViewModel
{
    public List<StorageProvider> Providers { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? Search { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public int EnabledCount { get; set; }
    public int LocalCount { get; set; }
    public int S3Count { get; set; }
    public int AzureCount { get; set; }
}

public class StorageProviderFormViewModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public StorageType Type { get; set; } = StorageType.Local;
    public string? Endpoint { get; set; }
    public string? BucketOrContainer { get; set; }
    public string? RootPath { get; set; }
    public string? CredentialsReference { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsImmutable { get; set; }
    public int? QuotaGB { get; set; }
}
