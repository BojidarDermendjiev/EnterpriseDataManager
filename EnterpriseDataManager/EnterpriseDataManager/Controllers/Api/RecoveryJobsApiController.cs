namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Filters;

/// <summary>
/// API controller for managing recovery jobs.
/// </summary>
[Route("api/recovery-jobs")]
public class RecoveryJobsApiController : ApiBaseController
{
    private readonly IRecoveryService _recoveryService;
    private readonly IRecoveryJobRepository _recoveryJobRepository;
    private readonly ILogger<RecoveryJobsApiController> _logger;

    public RecoveryJobsApiController(
        IRecoveryService recoveryService,
        IRecoveryJobRepository recoveryJobRepository,
        ILogger<RecoveryJobsApiController> logger)
    {
        _recoveryService = recoveryService;
        _recoveryJobRepository = recoveryJobRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all recovery jobs with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<RecoveryJobSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedApiResponse<RecoveryJobSummaryDto>>> GetAll(
        [FromQuery] Guid? archiveJobId = null,
        [FromQuery] ArchiveStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _recoveryJobRepository.GetAllAsync(cancellationToken);

        if (archiveJobId.HasValue)
        {
            jobs = jobs.Where(j => j.ArchiveJobId == archiveJobId.Value).ToList();
        }

        if (status.HasValue)
        {
            jobs = jobs.Where(j => j.Status == status.Value).ToList();
        }

        var total = jobs.Count;
        var pagedJobs = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new RecoveryJobSummaryDto
            {
                Id = j.Id,
                ArchiveJobId = j.ArchiveJobId,
                Status = j.Status,
                ProgressPercentage = j.TotalItems > 0 ? (double)j.RecoveredItems / j.TotalItems * 100 : 0,
                StartedAt = j.StartedAt
            })
            .ToList();

        return Success(pagedJobs, total, page, pageSize);
    }

    /// <summary>
    /// Gets a specific recovery job by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RecoveryJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecoveryJobDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _recoveryService.GetRecoveryStatusAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFoundResponse<RecoveryJobDto>($"Recovery job with ID {id} not found");
        }

        var dto = new RecoveryJobDto
        {
            Id = job.Id,
            ArchiveJobId = job.ArchiveJobId,
            DestinationPath = job.DestinationPath,
            Status = job.Status,
            FailureReason = job.FailureReason,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            TotalItems = job.TotalItems,
            RecoveredItems = job.RecoveredItems,
            TotalBytes = job.TotalBytes,
            RecoveredBytes = job.RecoveredBytes,
            ProgressPercentage = job.TotalItems > 0 ? (double)job.RecoveredItems / job.TotalItems * 100 : 0,
            Duration = job.CompletedAt.HasValue && job.StartedAt.HasValue
                ? job.CompletedAt.Value - job.StartedAt.Value
                : null,
            CreatedAt = job.CreatedAt,
            CreatedBy = job.CreatedBy
        };

        return Success(dto);
    }

    /// <summary>
    /// Creates a new recovery job.
    /// </summary>
    [HttpPost]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<RecoveryJobDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecoveryJobDto>>> Create(
        [FromBody] CreateRecoveryJobDto dto,
        CancellationToken cancellationToken = default)
    {
        var job = await _recoveryService.CreateRecoveryJobAsync(
            dto.ArchiveJobId,
            dto.DestinationPath,
            cancellationToken);

        var result = new RecoveryJobDto
        {
            Id = job.Id,
            ArchiveJobId = job.ArchiveJobId,
            DestinationPath = job.DestinationPath,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            CreatedBy = job.CreatedBy
        };

        return Created(result, nameof(GetById), new { id = job.Id }, "Recovery job created successfully");
    }

    /// <summary>
    /// Starts a recovery job.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RecoveryJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecoveryJobDto>>> Start(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _recoveryService.StartRecoveryAsync(id, cancellationToken);

        var result = new RecoveryJobDto
        {
            Id = job.Id,
            ArchiveJobId = job.ArchiveJobId,
            Status = job.Status,
            StartedAt = job.StartedAt
        };

        return Success(result, "Recovery job started successfully");
    }

    /// <summary>
    /// Cancels a recovery job.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RecoveryJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecoveryJobDto>>> Cancel(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _recoveryService.CancelRecoveryAsync(id, cancellationToken);

        var result = new RecoveryJobDto
        {
            Id = job.Id,
            ArchiveJobId = job.ArchiveJobId,
            Status = job.Status
        };

        return Success(result, "Recovery job cancelled successfully");
    }

    /// <summary>
    /// Gets all running recovery jobs.
    /// </summary>
    [HttpGet("running")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecoveryJobSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecoveryJobSummaryDto>>>> GetRunning(
        CancellationToken cancellationToken = default)
    {
        var jobs = await _recoveryService.GetRunningRecoveriesAsync(cancellationToken);

        var result = jobs.Select(j => new RecoveryJobSummaryDto
        {
            Id = j.Id,
            ArchiveJobId = j.ArchiveJobId,
            Status = j.Status,
            ProgressPercentage = j.TotalItems > 0 ? (double)j.RecoveredItems / j.TotalItems * 100 : 0,
            StartedAt = j.StartedAt
        }).ToList();

        return Success<IReadOnlyList<RecoveryJobSummaryDto>>(result);
    }

    /// <summary>
    /// Validates the integrity of an archive before recovery.
    /// </summary>
    [HttpGet("validate-archive/{archiveJobId:guid}")]
    [ValidateGuid("archiveJobId")]
    [ProducesResponseType(typeof(ApiResponse<ArchiveValidationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ArchiveValidationResult>>> ValidateArchive(
        Guid archiveJobId,
        CancellationToken cancellationToken = default)
    {
        var isValid = await _recoveryService.ValidateArchiveIntegrityAsync(archiveJobId, cancellationToken);

        var result = new ArchiveValidationResult
        {
            ArchiveJobId = archiveJobId,
            IsValid = isValid,
            ValidatedAt = DateTimeOffset.UtcNow
        };

        return Success(result);
    }

    /// <summary>
    /// Gets recovery jobs for a specific archive.
    /// </summary>
    [HttpGet("by-archive/{archiveJobId:guid}")]
    [ValidateGuid("archiveJobId")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecoveryJobSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecoveryJobSummaryDto>>>> GetByArchive(
        Guid archiveJobId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _recoveryJobRepository.GetByArchiveJobAsync(archiveJobId, cancellationToken);

        var result = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .Select(j => new RecoveryJobSummaryDto
            {
                Id = j.Id,
                ArchiveJobId = j.ArchiveJobId,
                Status = j.Status,
                ProgressPercentage = j.TotalItems > 0 ? (double)j.RecoveredItems / j.TotalItems * 100 : 0,
                StartedAt = j.StartedAt
            }).ToList();

        return Success<IReadOnlyList<RecoveryJobSummaryDto>>(result);
    }
}

/// <summary>
/// Result of archive validation.
/// </summary>
public record ArchiveValidationResult
{
    public Guid ArchiveJobId { get; init; }
    public bool IsValid { get; init; }
    public DateTimeOffset ValidatedAt { get; init; }
    public string? ErrorMessage { get; init; }
}
