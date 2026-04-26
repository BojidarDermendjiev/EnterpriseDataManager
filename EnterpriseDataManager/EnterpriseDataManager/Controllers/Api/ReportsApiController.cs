namespace EnterpriseDataManager.Controllers.Api;

using Asp.Versioning;
using EnterpriseDataManager.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Authorize]
public sealed class ReportsApiController : ApiBaseController
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportsApiController> _logger;

    public ReportsApiController(IReportService reportService, ILogger<ReportsApiController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    [HttpGet("archive-jobs/excel")]
    public async Task<IActionResult> ArchiveJobsExcel(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTimeOffset.UtcNow.AddYears(-1);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var bytes = await _reportService.GenerateArchiveJobsExcelAsync(fromDate, toDate, cancellationToken);
        var filename = $"archive-jobs-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.xls";
        return File(bytes, "application/vnd.ms-excel", filename);
    }

    [HttpGet("archive-jobs/pdf")]
    public async Task<IActionResult> ArchiveJobsPdf(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTimeOffset.UtcNow.AddYears(-1);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var bytes = await _reportService.GenerateArchiveJobsPdfAsync(fromDate, toDate, cancellationToken);
        var filename = $"archive-jobs-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpGet("audit-log/pdf")]
    public async Task<IActionResult> AuditLogPdf(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTimeOffset.UtcNow.AddYears(-1);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var bytes = await _reportService.GenerateAuditLogPdfAsync(fromDate, toDate, cancellationToken);
        var filename = $"audit-log-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpGet("audit-log/excel")]
    public async Task<IActionResult> AuditLogExcel(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var fromDate = from ?? DateTimeOffset.UtcNow.AddYears(-1);
        var toDate = to ?? DateTimeOffset.UtcNow;

        var bytes = await _reportService.GenerateAuditLogExcelAsync(fromDate, toDate, cancellationToken);
        var filename = $"audit-log-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.xls";
        return File(bytes, "application/vnd.ms-excel", filename);
    }
}
