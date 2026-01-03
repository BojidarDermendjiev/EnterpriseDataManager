namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Filters;

/// <summary>
/// API controller for managing retention policies.
/// </summary>
[Route("api/retention-policies")]
public class RetentionPoliciesApiController : ApiBaseController
{
    private readonly IRetentionPolicyService _retentionPolicyService;
    private readonly IRetentionPolicyRepository _retentionPolicyRepository;
    private readonly ILogger<RetentionPoliciesApiController> _logger;

    public RetentionPoliciesApiController(
        IRetentionPolicyService retentionPolicyService,
        IRetentionPolicyRepository retentionPolicyRepository,
        ILogger<RetentionPoliciesApiController> logger)
    {
        _retentionPolicyService = retentionPolicyService;
        _retentionPolicyRepository = retentionPolicyRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all retention policies with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<RetentionPolicySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedApiResponse<RetentionPolicySummaryDto>>> GetAll(
        [FromQuery] bool? isLegalHold = null,
        [FromQuery] bool? isImmutable = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var policies = await _retentionPolicyRepository.GetAllAsync(cancellationToken);

        if (isLegalHold.HasValue)
        {
            policies = policies.Where(p => p.IsLegalHold == isLegalHold.Value).ToList();
        }

        if (isImmutable.HasValue)
        {
            policies = policies.Where(p => p.IsImmutable == isImmutable.Value).ToList();
        }

        var total = policies.Count;
        var pagedPolicies = policies
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new RetentionPolicySummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                RetentionDays = (int)p.RetentionPeriod.TotalDays,
                IsLegalHold = p.IsLegalHold,
                IsImmutable = p.IsImmutable
            })
            .ToList();

        return Success(pagedPolicies, total, page, pageSize);
    }

    /// <summary>
    /// Gets a specific retention policy by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RetentionPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RetentionPolicyDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var policy = await _retentionPolicyRepository.GetByIdAsync(id, cancellationToken);
        if (policy == null)
        {
            return NotFoundResponse<RetentionPolicyDto>($"Retention policy with ID {id} not found");
        }

        var dto = new RetentionPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            RetentionPeriod = policy.RetentionPeriod,
            RetentionDays = (int)policy.RetentionPeriod.TotalDays,
            IsLegalHold = policy.IsLegalHold,
            IsImmutable = policy.IsImmutable,
            Scope = policy.Scope,
            CreatedAt = policy.CreatedAt,
            CreatedBy = policy.CreatedBy,
            UpdatedAt = policy.UpdatedAt,
            UpdatedBy = policy.UpdatedBy
        };

        return Success(dto);
    }

    /// <summary>
    /// Creates a new retention policy.
    /// </summary>
    [HttpPost]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<RetentionPolicyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RetentionPolicyDto>>> Create(
        [FromBody] CreateRetentionPolicyDto dto,
        CancellationToken cancellationToken = default)
    {
        var retentionPeriod = TimeSpan.FromDays(dto.RetentionDays);
        var policy = await _retentionPolicyService.CreatePolicyAsync(dto.Name, retentionPeriod, cancellationToken);

        if (!string.IsNullOrEmpty(dto.Description))
        {
            await _retentionPolicyService.UpdatePolicyAsync(policy.Id, dto.Name, dto.Description, cancellationToken);
        }

        var result = new RetentionPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            RetentionPeriod = policy.RetentionPeriod,
            RetentionDays = (int)policy.RetentionPeriod.TotalDays,
            IsLegalHold = policy.IsLegalHold,
            IsImmutable = policy.IsImmutable,
            CreatedAt = policy.CreatedAt
        };

        return Created(result, nameof(GetById), new { id = policy.Id }, "Retention policy created successfully");
    }

    /// <summary>
    /// Updates an existing retention policy.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ValidateModel]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RetentionPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RetentionPolicyDto>>> Update(
        Guid id,
        [FromBody] UpdateRetentionPolicyDto dto,
        CancellationToken cancellationToken = default)
    {
        var existing = await _retentionPolicyRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFoundResponse<RetentionPolicyDto>($"Retention policy with ID {id} not found");
        }

        if (existing.IsImmutable)
        {
            return BadRequestResponse<RetentionPolicyDto>("Cannot modify an immutable retention policy");
        }

        var policy = await _retentionPolicyService.UpdatePolicyAsync(id, dto.Name, dto.Description, cancellationToken);

        if (dto.RetentionDays.HasValue)
        {
            await _retentionPolicyService.SetRetentionPeriodAsync(id, TimeSpan.FromDays(dto.RetentionDays.Value), cancellationToken);
        }

        var result = new RetentionPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            Description = policy.Description,
            RetentionPeriod = policy.RetentionPeriod,
            RetentionDays = (int)policy.RetentionPeriod.TotalDays,
            IsLegalHold = policy.IsLegalHold,
            IsImmutable = policy.IsImmutable,
            UpdatedAt = policy.UpdatedAt
        };

        return Success(result, "Retention policy updated successfully");
    }

    /// <summary>
    /// Deletes a retention policy.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _retentionPolicyRepository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound();
        }

        if (existing.IsImmutable)
        {
            return BadRequest(new { message = "Cannot delete an immutable retention policy" });
        }

        await _retentionPolicyService.DeletePolicyAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Enables legal hold on a retention policy.
    /// </summary>
    [HttpPost("{id:guid}/legal-hold/enable")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RetentionPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RetentionPolicyDto>>> EnableLegalHold(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var policy = await _retentionPolicyService.EnableLegalHoldAsync(id, cancellationToken);

        var result = new RetentionPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            IsLegalHold = policy.IsLegalHold
        };

        return Success(result, "Legal hold enabled successfully");
    }

    /// <summary>
    /// Disables legal hold on a retention policy.
    /// </summary>
    [HttpPost("{id:guid}/legal-hold/disable")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RetentionPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RetentionPolicyDto>>> DisableLegalHold(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var policy = await _retentionPolicyService.DisableLegalHoldAsync(id, cancellationToken);

        var result = new RetentionPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            IsLegalHold = policy.IsLegalHold
        };

        return Success(result, "Legal hold disabled successfully");
    }

    /// <summary>
    /// Makes a retention policy immutable.
    /// </summary>
    [HttpPost("{id:guid}/make-immutable")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<RetentionPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RetentionPolicyDto>>> MakeImmutable(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var policy = await _retentionPolicyService.MakeImmutableAsync(id, cancellationToken);

        var result = new RetentionPolicyDto
        {
            Id = policy.Id,
            Name = policy.Name,
            IsImmutable = policy.IsImmutable
        };

        return Success(result, "Retention policy is now immutable");
    }

    /// <summary>
    /// Gets all retention policies.
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RetentionPolicySummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RetentionPolicySummaryDto>>>> GetAllPolicies(
        CancellationToken cancellationToken = default)
    {
        var policies = await _retentionPolicyService.GetAllPoliciesAsync(cancellationToken);

        var result = policies.Select(p => new RetentionPolicySummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            RetentionDays = (int)p.RetentionPeriod.TotalDays,
            IsLegalHold = p.IsLegalHold,
            IsImmutable = p.IsImmutable
        }).ToList();

        return Success<IReadOnlyList<RetentionPolicySummaryDto>>(result);
    }
}
