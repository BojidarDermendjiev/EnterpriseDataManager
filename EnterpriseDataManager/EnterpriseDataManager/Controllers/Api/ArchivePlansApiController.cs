namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Filters;

/// <summary>
/// API controller for managing archive plans.
/// </summary>
[Route("api/archive-plans")]
public class ArchivePlansApiController : ApiBaseController
{
    private readonly IArchivePlanService _archivePlanService;
    private readonly IArchivePlanRepository _archivePlanRepository;
    private readonly ILogger<ArchivePlansApiController> _logger;

    public ArchivePlansApiController(
        IArchivePlanService archivePlanService,
        IArchivePlanRepository archivePlanRepository,
        ILogger<ArchivePlansApiController> logger)
    {
        _archivePlanService = archivePlanService;
        _archivePlanRepository = archivePlanRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all archive plans with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<ArchivePlanSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedApiResponse<ArchivePlanSummaryDto>>> GetAll(
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var plans = await _archivePlanRepository.GetAllAsync(cancellationToken);

        if (isActive.HasValue)
        {
            plans = plans.Where(p => p.IsActive == isActive.Value).ToList();
        }

        var total = plans.Count;
        var pagedPlans = plans
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ArchivePlanSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                IsActive = p.IsActive,
                SecurityLevel = p.SecurityLevel,
                LastRunAt = p.LastRunAt,
                NextRunAt = p.NextRunAt
            })
            .ToList();

        return Success(pagedPlans, total, page, pageSize);
    }

    /// <summary>
    /// Gets a specific archive plan by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ArchivePlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ValidateGuid("id")]
    public async Task<ActionResult<ApiResponse<ArchivePlanDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var plan = await _archivePlanRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            return NotFoundResponse<ArchivePlanDto>($"Archive plan with ID {id} not found");
        }

        var dto = new ArchivePlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            Schedule = plan.Schedule?.Expression,
            ScheduleDescription = plan.Schedule?.Description,
            SourcePath = plan.SourcePath,
            IsActive = plan.IsActive,
            SecurityLevel = plan.SecurityLevel,
            StorageProviderId = plan.StorageProviderId,
            RetentionPolicyId = plan.RetentionPolicyId,
            LastRunAt = plan.LastRunAt,
            NextRunAt = plan.NextRunAt,
            CreatedAt = plan.CreatedAt,
            CreatedBy = plan.CreatedBy,
            UpdatedAt = plan.UpdatedAt,
            UpdatedBy = plan.UpdatedBy
        };

        return Success(dto);
    }

    /// <summary>
    /// Creates a new archive plan.
    /// </summary>
    [HttpPost]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<ArchivePlanDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ArchivePlanDto>>> Create(
        [FromBody] CreateArchivePlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var plan = await _archivePlanService.CreatePlanAsync(
            dto.Name,
            dto.SourcePath,
            dto.SecurityLevel,
            cancellationToken);

        if (!string.IsNullOrEmpty(dto.Schedule))
        {
            await _archivePlanService.SetScheduleAsync(plan.Id, dto.Schedule, dto.ScheduleDescription, cancellationToken);
        }

        if (dto.StorageProviderId.HasValue)
        {
            await _archivePlanService.SetStorageProviderAsync(plan.Id, dto.StorageProviderId.Value, cancellationToken);
        }

        if (dto.RetentionPolicyId.HasValue)
        {
            await _archivePlanService.SetRetentionPolicyAsync(plan.Id, dto.RetentionPolicyId.Value, cancellationToken);
        }

        var result = new ArchivePlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            SourcePath = plan.SourcePath,
            SecurityLevel = plan.SecurityLevel,
            IsActive = plan.IsActive,
            CreatedAt = plan.CreatedAt,
            CreatedBy = plan.CreatedBy
        };

        return Created(result, nameof(GetById), new { id = plan.Id }, "Archive plan created successfully");
    }

    /// <summary>
    /// Updates an existing archive plan.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ValidateModel]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<ArchivePlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ArchivePlanDto>>> Update(
        Guid id,
        [FromBody] UpdateArchivePlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var existing = await _archivePlanRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFoundResponse<ArchivePlanDto>($"Archive plan with ID {id} not found");
        }

        var plan = await _archivePlanService.UpdatePlanAsync(id, dto.Name, dto.Description, cancellationToken);

        if (!string.IsNullOrEmpty(dto.Schedule))
        {
            await _archivePlanService.SetScheduleAsync(id, dto.Schedule, dto.ScheduleDescription, cancellationToken);
        }

        if (dto.StorageProviderId.HasValue)
        {
            await _archivePlanService.SetStorageProviderAsync(id, dto.StorageProviderId.Value, cancellationToken);
        }

        if (dto.RetentionPolicyId.HasValue)
        {
            await _archivePlanService.SetRetentionPolicyAsync(id, dto.RetentionPolicyId.Value, cancellationToken);
        }

        var result = new ArchivePlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            SourcePath = plan.SourcePath,
            SecurityLevel = plan.SecurityLevel,
            IsActive = plan.IsActive,
            UpdatedAt = plan.UpdatedAt,
            UpdatedBy = plan.UpdatedBy
        };

        return Success(result, "Archive plan updated successfully");
    }

    /// <summary>
    /// Deletes an archive plan.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _archivePlanRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound();
        }

        await _archivePlanService.DeletePlanAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Activates an archive plan.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<ArchivePlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ArchivePlanDto>>> Activate(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var plan = await _archivePlanService.ActivatePlanAsync(id, cancellationToken);

        var result = new ArchivePlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            IsActive = plan.IsActive
        };

        return Success(result, "Archive plan activated successfully");
    }

    /// <summary>
    /// Deactivates an archive plan.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<ArchivePlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ArchivePlanDto>>> Deactivate(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var plan = await _archivePlanService.DeactivatePlanAsync(id, cancellationToken);

        var result = new ArchivePlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            IsActive = plan.IsActive
        };

        return Success(result, "Archive plan deactivated successfully");
    }

    /// <summary>
    /// Gets all active archive plans.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ArchivePlanSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ArchivePlanSummaryDto>>>> GetActive(
        CancellationToken cancellationToken = default)
    {
        var plans = await _archivePlanService.GetActivePlansAsync(cancellationToken);

        var result = plans.Select(p => new ArchivePlanSummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            IsActive = p.IsActive,
            SecurityLevel = p.SecurityLevel,
            LastRunAt = p.LastRunAt,
            NextRunAt = p.NextRunAt
        }).ToList();

        return Success<IReadOnlyList<ArchivePlanSummaryDto>>(result);
    }
}
