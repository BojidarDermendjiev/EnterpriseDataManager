namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Filters;

/// <summary>
/// API controller for managing storage providers.
/// </summary>
[Route("api/storage-providers")]
public class StorageProvidersApiController : ApiBaseController
{
    private readonly IStorageService _storageService;
    private readonly IStorageProviderRepository _storageProviderRepository;
    private readonly ILogger<StorageProvidersApiController> _logger;

    public StorageProvidersApiController(
        IStorageService storageService,
        IStorageProviderRepository storageProviderRepository,
        ILogger<StorageProvidersApiController> logger)
    {
        _storageService = storageService;
        _storageProviderRepository = storageProviderRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all storage providers with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<StorageProviderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedApiResponse<StorageProviderSummaryDto>>> GetAll(
        [FromQuery] StorageType? type = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var providers = await _storageProviderRepository.GetAllAsync(cancellationToken);

        if (type.HasValue)
        {
            providers = providers.Where(p => p.Type == type.Value).ToList();
        }

        if (isEnabled.HasValue)
        {
            providers = providers.Where(p => p.IsEnabled == isEnabled.Value).ToList();
        }

        var total = providers.Count;
        var pagedProviders = providers
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new StorageProviderSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                IsEnabled = p.IsEnabled,
                QuotaBytes = p.QuotaBytes
            })
            .ToList();

        return Success(pagedProviders, total, page, pageSize);
    }

    /// <summary>
    /// Gets a specific storage provider by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StorageProviderDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFoundResponse<StorageProviderDto>($"Storage provider with ID {id} not found");
        }

        var dto = new StorageProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            Description = provider.Description,
            Type = provider.Type,
            Endpoint = provider.Endpoint,
            BucketOrContainer = provider.BucketOrContainer,
            RootPath = provider.RootPath,
            HasCredentials = !string.IsNullOrEmpty(provider.CredentialsReference),
            IsImmutable = provider.IsImmutable,
            IsEnabled = provider.IsEnabled,
            QuotaBytes = provider.QuotaBytes,
            CreatedAt = provider.CreatedAt,
            CreatedBy = provider.CreatedBy,
            UpdatedAt = provider.UpdatedAt,
            UpdatedBy = provider.UpdatedBy
        };

        return Success(dto);
    }

    /// <summary>
    /// Creates a new local storage provider.
    /// </summary>
    [HttpPost("local")]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StorageProviderDto>>> CreateLocal(
        [FromBody] CreateLocalStorageProviderDto dto,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageService.CreateLocalProviderAsync(dto.Name, dto.RootPath, cancellationToken);

        if (dto.QuotaBytes.HasValue)
        {
            await _storageService.SetQuotaAsync(provider.Id, dto.QuotaBytes.Value, cancellationToken);
        }

        var result = new StorageProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            Type = provider.Type,
            RootPath = provider.RootPath,
            IsEnabled = provider.IsEnabled,
            CreatedAt = provider.CreatedAt
        };

        return Created(result, nameof(GetById), new { id = provider.Id }, "Local storage provider created successfully");
    }

    /// <summary>
    /// Creates a new S3 storage provider.
    /// </summary>
    [HttpPost("s3")]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StorageProviderDto>>> CreateS3(
        [FromBody] CreateS3StorageProviderDto dto,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageService.CreateS3ProviderAsync(
            dto.Name,
            dto.Endpoint,
            dto.Bucket,
            dto.CredentialsReference,
            cancellationToken);

        if (dto.QuotaBytes.HasValue)
        {
            await _storageService.SetQuotaAsync(provider.Id, dto.QuotaBytes.Value, cancellationToken);
        }

        var result = new StorageProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            Type = provider.Type,
            Endpoint = provider.Endpoint,
            BucketOrContainer = provider.BucketOrContainer,
            IsEnabled = provider.IsEnabled,
            CreatedAt = provider.CreatedAt
        };

        return Created(result, nameof(GetById), new { id = provider.Id }, "S3 storage provider created successfully");
    }

    /// <summary>
    /// Creates a new Azure Blob storage provider.
    /// </summary>
    [HttpPost("azure-blob")]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StorageProviderDto>>> CreateAzureBlob(
        [FromBody] CreateAzureBlobStorageProviderDto dto,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageService.CreateAzureBlobProviderAsync(
            dto.Name,
            dto.Endpoint,
            dto.Container,
            dto.CredentialsReference,
            cancellationToken);

        if (dto.QuotaBytes.HasValue)
        {
            await _storageService.SetQuotaAsync(provider.Id, dto.QuotaBytes.Value, cancellationToken);
        }

        var result = new StorageProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            Type = provider.Type,
            Endpoint = provider.Endpoint,
            BucketOrContainer = provider.BucketOrContainer,
            IsEnabled = provider.IsEnabled,
            CreatedAt = provider.CreatedAt
        };

        return Created(result, nameof(GetById), new { id = provider.Id }, "Azure Blob storage provider created successfully");
    }

    /// <summary>
    /// Updates an existing storage provider.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ValidateModel]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StorageProviderDto>>> Update(
        Guid id,
        [FromBody] UpdateStorageProviderDto dto,
        CancellationToken cancellationToken = default)
    {
        var existing = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFoundResponse<StorageProviderDto>($"Storage provider with ID {id} not found");
        }

        var provider = await _storageService.UpdateProviderAsync(id, dto.Name, dto.Description, cancellationToken);

        if (dto.QuotaBytes.HasValue)
        {
            await _storageService.SetQuotaAsync(id, dto.QuotaBytes.Value, cancellationToken);
        }

        var result = new StorageProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            Description = provider.Description,
            Type = provider.Type,
            IsEnabled = provider.IsEnabled,
            QuotaBytes = provider.QuotaBytes,
            UpdatedAt = provider.UpdatedAt
        };

        return Success(result, "Storage provider updated successfully");
    }

    /// <summary>
    /// Deletes a storage provider.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound();
        }

        await _storageService.DeleteProviderAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Enables a storage provider.
    /// </summary>
    [HttpPost("{id:guid}/enable")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StorageProviderDto>>> Enable(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageService.EnableProviderAsync(id, cancellationToken);

        var result = new StorageProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            IsEnabled = provider.IsEnabled
        };

        return Success(result, "Storage provider enabled successfully");
    }

    /// <summary>
    /// Disables a storage provider.
    /// </summary>
    [HttpPost("{id:guid}/disable")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<StorageProviderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StorageProviderDto>>> Disable(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageService.DisableProviderAsync(id, cancellationToken);

        var result = new StorageProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            IsEnabled = provider.IsEnabled
        };

        return Success(result, "Storage provider disabled successfully");
    }

    /// <summary>
    /// Checks the health of a storage provider.
    /// </summary>
    [HttpGet("{id:guid}/health")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<StorageHealthDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StorageHealthDto>>> CheckHealth(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFoundResponse<StorageHealthDto>($"Storage provider with ID {id} not found");
        }

        var health = await _storageService.CheckHealthAsync(id, cancellationToken);

        var result = new StorageHealthDto
        {
            ProviderId = health.ProviderId,
            ProviderName = provider.Name,
            IsHealthy = health.IsHealthy,
            IsReachable = health.IsReachable,
            Latency = health.Latency,
            ErrorMessage = health.ErrorMessage,
            CheckedAt = health.CheckedAt
        };

        return Success(result);
    }

    /// <summary>
    /// Gets the usage information for a storage provider.
    /// </summary>
    [HttpGet("{id:guid}/usage")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<StorageUsageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StorageUsageDto>>> GetUsage(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _storageProviderRepository.GetByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFoundResponse<StorageUsageDto>($"Storage provider with ID {id} not found");
        }

        var usage = await _storageService.GetUsageInfoAsync(id, cancellationToken);

        var result = new StorageUsageDto
        {
            ProviderId = usage.ProviderId,
            ProviderName = provider.Name,
            UsedBytes = usage.UsedBytes,
            QuotaBytes = usage.QuotaBytes,
            UsagePercentage = usage.UsagePercentage,
            FileCount = usage.FileCount,
            CalculatedAt = usage.CalculatedAt
        };

        return Success(result);
    }

    /// <summary>
    /// Gets all enabled storage providers.
    /// </summary>
    [HttpGet("enabled")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StorageProviderSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StorageProviderSummaryDto>>>> GetEnabled(
        CancellationToken cancellationToken = default)
    {
        var providers = await _storageService.GetEnabledProvidersAsync(cancellationToken);

        var result = providers.Select(p => new StorageProviderSummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            Type = p.Type,
            IsEnabled = p.IsEnabled,
            QuotaBytes = p.QuotaBytes
        }).ToList();

        return Success<IReadOnlyList<StorageProviderSummaryDto>>(result);
    }
}
