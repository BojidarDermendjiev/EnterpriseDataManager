namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Filters;

/// <summary>
/// API controller for managing archive jobs.
/// </summary>
[Route("api/archive-jobs")]
public class ArchiveJobsApiController : ApiBaseController
{
    private readonly IArchivalService _archivalService;
    private readonly IArchiveJobRepository _archiveJobRepository;
    private readonly ILogger<ArchiveJobsApiController> _logger;

    public ArchiveJobsApiController(
        IArchivalService archivalService,
        IArchiveJobRepository archiveJobRepository,
        ILogger<ArchiveJobsApiController> logger)
    {
        _archivalService = archivalService;
        _archiveJobRepository = archiveJobRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all archive jobs with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<ArchiveJobSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedApiResponse<ArchiveJobSummaryDto>>> GetAll(
        [FromQuery] Guid? planId = null,
        [FromQuery] ArchiveStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _archiveJobRepository.GetAllAsync(cancellationToken);

        if (planId.HasValue)
        {
            jobs = jobs.Where(j => j.ArchivePlanId == planId.Value).ToList();
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
            .Select(j => new ArchiveJobSummaryDto
            {
                Id = j.Id,
                ArchivePlanId = j.ArchivePlanId,
                Status = j.Status,
                Priority = j.Priority,
                ProgressPercentage = j.TotalItemCount > 0 ? (double)j.ProcessedItemCount / j.TotalItemCount * 100 : 0,
                StartedAt = j.StartedAt
            })
            .ToList();

        return Success(pagedJobs, total, page, pageSize);
    }

    /// <summary>
    /// Gets a specific archive job by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<ArchiveJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ArchiveJobDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _archivalService.GetJobStatusAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFoundResponse<ArchiveJobDto>($"Archive job with ID {id} not found");
        }

        var dto = new ArchiveJobDto
        {
            Id = job.Id,
            ArchivePlanId = job.ArchivePlanId,
            Status = job.Status,
            Priority = job.Priority,
            ScheduledAt = job.ScheduledAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            FailureReason = job.FailureReason,
            TotalItemCount = job.TotalItemCount,
            ProcessedItemCount = job.ProcessedItemCount,
            FailedItemCount = job.FailedItemCount,
            TotalBytes = job.TotalBytes,
            ProcessedBytes = job.ProcessedBytes,
            TargetPath = job.TargetPath,
            ProgressPercentage = job.TotalItemCount > 0 ? (double)job.ProcessedItemCount / job.TotalItemCount * 100 : 0,
            Duration = job.CompletedAt.HasValue && job.StartedAt.HasValue
                ? job.CompletedAt.Value - job.StartedAt.Value
                : null,
            CreatedAt = job.CreatedAt,
            CreatedBy = job.CreatedBy
        };

        return Success(dto);
    }

    /// <summary>
    /// Creates a new archive job.
    /// </summary>
    [HttpPost]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<ArchiveJobDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ArchiveJobDto>>> Create(
        [FromBody] CreateArchiveJobDto dto,
        CancellationToken cancellationToken = default)
    {
        var job = await _archivalService.CreateJobAsync(dto.ArchivePlanId, dto.Priority, cancellationToken);

        var result = new ArchiveJobDto
        {
            Id = job.Id,
            ArchivePlanId = job.ArchivePlanId,
            Status = job.Status,
            Priority = job.Priority,
            CreatedAt = job.CreatedAt,
            CreatedBy = job.CreatedBy
        };

        return Created(result, nameof(GetById), new { id = job.Id }, "Archive job created successfully");
    }

    /// <summary>
    /// Starts an archive job.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<ArchiveJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ArchiveJobDto>>> Start(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _archivalService.StartJobAsync(id, cancellationToken);

        var result = new ArchiveJobDto
        {
            Id = job.Id,
            ArchivePlanId = job.ArchivePlanId,
            Status = job.Status,
            StartedAt = job.StartedAt
        };

        return Success(result, "Archive job started successfully");
    }

    /// <summary>
    /// Cancels an archive job.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<ArchiveJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ArchiveJobDto>>> Cancel(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _archivalService.CancelJobAsync(id, cancellationToken);

        var result = new ArchiveJobDto
        {
            Id = job.Id,
            ArchivePlanId = job.ArchivePlanId,
            Status = job.Status
        };

        return Success(result, "Archive job cancelled successfully");
    }

    /// <summary>
    /// Gets the progress of an archive job.
    /// </summary>
    [HttpGet("{id:guid}/progress")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<JobProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JobProgressDto>>> GetProgress(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _archivalService.GetJobStatusAsync(id, cancellationToken);
        if (job == null)
        {
            return NotFoundResponse<JobProgressDto>($"Archive job with ID {id} not found");
        }

        var progress = new JobProgressDto
        {
            JobId = job.Id,
            Status = job.Status,
            TotalItems = job.TotalItemCount,
            ProcessedItems = job.ProcessedItemCount,
            FailedItems = job.FailedItemCount,
            TotalBytes = job.TotalBytes,
            ProcessedBytes = job.ProcessedBytes,
            ProgressPercentage = job.TotalItemCount > 0 ? (double)job.ProcessedItemCount / job.TotalItemCount * 100 : 0,
            ElapsedTime = job.StartedAt.HasValue ? DateTimeOffset.UtcNow - job.StartedAt.Value : null
        };

        return Success(progress);
    }

    /// <summary>
    /// Gets all running archive jobs.
    /// </summary>
    [HttpGet("running")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ArchiveJobSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ArchiveJobSummaryDto>>>> GetRunning(
        CancellationToken cancellationToken = default)
    {
        var jobs = await _archivalService.GetRunningJobsAsync(cancellationToken);

        var result = jobs.Select(j => new ArchiveJobSummaryDto
        {
            Id = j.Id,
            ArchivePlanId = j.ArchivePlanId,
            Status = j.Status,
            Priority = j.Priority,
            ProgressPercentage = j.TotalItemCount > 0 ? (double)j.ProcessedItemCount / j.TotalItemCount * 100 : 0,
            StartedAt = j.StartedAt
        }).ToList();

        return Success<IReadOnlyList<ArchiveJobSummaryDto>>(result);
    }

    /// <summary>
    /// Gets archive jobs for a specific plan.
    /// </summary>
    [HttpGet("by-plan/{planId:guid}")]
    [ValidateGuid("planId")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ArchiveJobSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ArchiveJobSummaryDto>>>> GetByPlan(
        Guid planId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _archiveJobRepository.GetByPlanAsync(planId, cancellationToken);

        var result = jobs
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .Select(j => new ArchiveJobSummaryDto
            {
                Id = j.Id,
                ArchivePlanId = j.ArchivePlanId,
                Status = j.Status,
                Priority = j.Priority,
                ProgressPercentage = j.TotalItemCount > 0 ? (double)j.ProcessedItemCount / j.TotalItemCount * 100 : 0,
                StartedAt = j.StartedAt
            }).ToList();

        return Success<IReadOnlyList<ArchiveJobSummaryDto>>(result);
    }
}
