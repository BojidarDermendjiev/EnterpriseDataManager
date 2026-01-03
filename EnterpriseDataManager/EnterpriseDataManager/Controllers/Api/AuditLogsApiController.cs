namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Application.DTOs;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Filters;

/// <summary>
/// API controller for managing audit logs.
/// </summary>
[Route("api/audit-logs")]
public class AuditLogsApiController : ApiBaseController
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditLogsApiController> _logger;

    public AuditLogsApiController(
        IAuditService auditService,
        ILogger<AuditLogsApiController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Searches audit logs with filtering criteria.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<AuditRecordSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedApiResponse<AuditRecordSummaryDto>>> Search(
        [FromQuery] AuditSearchDto searchDto,
        CancellationToken cancellationToken = default)
    {
        var criteria = new AuditSearchCriteria(
            Actor: searchDto.Actor,
            Action: searchDto.Action,
            ResourceType: searchDto.ResourceType,
            ResourceId: searchDto.ResourceId,
            Success: searchDto.Success,
            From: searchDto.From,
            To: searchDto.To,
            IpAddress: searchDto.IpAddress,
            CorrelationId: searchDto.CorrelationId,
            Skip: searchDto.Skip,
            Take: searchDto.Take);

        var records = await _auditService.SearchAuditLogsAsync(criteria, cancellationToken);

        var result = records.Select(r => new AuditRecordSummaryDto
        {
            Id = r.Id,
            Timestamp = r.Timestamp,
            Actor = r.Actor,
            Action = r.Action,
            Success = r.Success
        }).ToList();

        // For pagination, we estimate total based on results
        var total = searchDto.Skip + result.Count + (result.Count == searchDto.Take ? 1 : 0);
        var page = (searchDto.Skip / searchDto.Take) + 1;

        return Success(result, total, page, searchDto.Take);
    }

    /// <summary>
    /// Gets a specific audit record by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ValidateGuid("id")]
    [ProducesResponseType(typeof(ApiResponse<AuditRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AuditRecordDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var criteria = new AuditSearchCriteria(Take: 1);
        var records = await _auditService.SearchAuditLogsAsync(criteria, cancellationToken);
        var record = records.FirstOrDefault(r => r.Id == id);

        if (record == null)
        {
            return NotFoundResponse<AuditRecordDto>($"Audit record with ID {id} not found");
        }

        var dto = new AuditRecordDto
        {
            Id = record.Id,
            Timestamp = record.Timestamp,
            Actor = record.Actor,
            Action = record.Action,
            ResourceType = record.ResourceType,
            ResourceId = record.ResourceId,
            Success = record.Success,
            Details = record.Details,
            IpAddress = record.IpAddress,
            UserAgent = record.UserAgent,
            CorrelationId = record.CorrelationId
        };

        return Success(dto);
    }

    /// <summary>
    /// Gets the audit trail for a specific resource.
    /// </summary>
    [HttpGet("resource/{resourceType}/{resourceId}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditRecordSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditRecordSummaryDto>>>> GetResourceAuditTrail(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var records = await _auditService.GetAuditTrailAsync(resourceType, resourceId, cancellationToken);

        var result = records.Select(r => new AuditRecordSummaryDto
        {
            Id = r.Id,
            Timestamp = r.Timestamp,
            Actor = r.Actor,
            Action = r.Action,
            Success = r.Success
        }).ToList();

        return Success<IReadOnlyList<AuditRecordSummaryDto>>(result);
    }

    /// <summary>
    /// Gets activity for a specific user.
    /// </summary>
    [HttpGet("user/{actor}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditRecordSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditRecordSummaryDto>>>> GetUserActivity(
        string actor,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var records = await _auditService.GetUserActivityAsync(actor, from, to, cancellationToken);

        var result = records.Select(r => new AuditRecordSummaryDto
        {
            Id = r.Id,
            Timestamp = r.Timestamp,
            Actor = r.Actor,
            Action = r.Action,
            Success = r.Success
        }).ToList();

        return Success<IReadOnlyList<AuditRecordSummaryDto>>(result);
    }

    /// <summary>
    /// Gets audit summary for a date range.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<AuditSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuditSummaryDto>>> GetSummary(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var summary = await _auditService.GetAuditSummaryAsync(fromDate, toDate, cancellationToken);

        var result = new AuditSummaryDto
        {
            TotalRecords = summary.TotalRecords,
            SuccessfulActions = summary.SuccessfulActions,
            FailedActions = summary.FailedActions,
            ActionCounts = summary.ActionCounts,
            ActorCounts = summary.ActorCounts,
            RecentFailures = summary.RecentFailures.Select(r => new AuditRecordSummaryDto
            {
                Id = r.Id,
                Timestamp = r.Timestamp,
                Actor = r.Actor,
                Action = r.Action,
                Success = r.Success
            }).ToList()
        };

        return Success(result);
    }

    /// <summary>
    /// Creates a manual audit record.
    /// </summary>
    [HttpPost]
    [ValidateModel]
    [ProducesResponseType(typeof(ApiResponse<AuditRecordDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuditRecordDto>>> Create(
        [FromBody] CreateAuditRecordDto dto,
        CancellationToken cancellationToken = default)
    {
        var record = await _auditService.LogWithDetailsAsync(
            dto.Actor,
            dto.Action,
            dto.Details ?? "",
            dto.Success,
            cancellationToken);

        var result = new AuditRecordDto
        {
            Id = record.Id,
            Timestamp = record.Timestamp,
            Actor = record.Actor,
            Action = record.Action,
            Success = record.Success,
            Details = record.Details
        };

        return Created(result, nameof(GetById), new { id = record.Id }, "Audit record created successfully");
    }

    /// <summary>
    /// Exports audit logs for a date range.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string format = "json",
        CancellationToken cancellationToken = default)
    {
        var criteria = new AuditSearchCriteria(
            From: from ?? DateTimeOffset.UtcNow.AddDays(-30),
            To: to ?? DateTimeOffset.UtcNow,
            Take: 10000);

        var records = await _auditService.SearchAuditLogsAsync(criteria, cancellationToken);

        var exportData = records.Select(r => new
        {
            r.Id,
            r.Timestamp,
            r.Actor,
            r.Action,
            r.ResourceType,
            r.ResourceId,
            r.Success,
            r.Details,
            r.IpAddress
        });

        if (format.ToLower() == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,Timestamp,Actor,Action,ResourceType,ResourceId,Success,IpAddress");
            foreach (var record in exportData)
            {
                csv.AppendLine($"\"{record.Id}\",\"{record.Timestamp:O}\",\"{record.Actor}\",\"{record.Action}\",\"{record.ResourceType}\",\"{record.ResourceId}\",{record.Success},\"{record.IpAddress}\"");
            }

            return File(
                System.Text.Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                $"audit-logs-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        return File(
            System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            $"audit-logs-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    /// <summary>
    /// Gets recent audit logs.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditRecordSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditRecordSummaryDto>>>> GetRecent(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var criteria = new AuditSearchCriteria(
            From: DateTimeOffset.UtcNow.AddHours(-24),
            Take: limit);

        var records = await _auditService.SearchAuditLogsAsync(criteria, cancellationToken);

        var result = records.Select(r => new AuditRecordSummaryDto
        {
            Id = r.Id,
            Timestamp = r.Timestamp,
            Actor = r.Actor,
            Action = r.Action,
            Success = r.Success
        }).ToList();

        return Success<IReadOnlyList<AuditRecordSummaryDto>>(result);
    }

    /// <summary>
    /// Gets failed actions for monitoring.
    /// </summary>
    [HttpGet("failures")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditRecordDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditRecordDto>>>> GetFailures(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var criteria = new AuditSearchCriteria(
            Success: false,
            From: from ?? DateTimeOffset.UtcNow.AddDays(-7),
            Take: limit);

        var records = await _auditService.SearchAuditLogsAsync(criteria, cancellationToken);

        var result = records.Select(r => new AuditRecordDto
        {
            Id = r.Id,
            Timestamp = r.Timestamp,
            Actor = r.Actor,
            Action = r.Action,
            ResourceType = r.ResourceType,
            ResourceId = r.ResourceId,
            Success = r.Success,
            Details = r.Details,
            IpAddress = r.IpAddress,
            CorrelationId = r.CorrelationId
        }).ToList();

        return Success<IReadOnlyList<AuditRecordDto>>(result);
    }
}
