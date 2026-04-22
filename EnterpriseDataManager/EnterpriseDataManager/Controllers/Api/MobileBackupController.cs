namespace EnterpriseDataManager.Controllers.Api;

using Asp.Versioning;
using EnterpriseDataManager.Application.DTOs.Mobile;
using EnterpriseDataManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mobile")]
public class MobileBackupController : ApiBaseController
{
    private const long MaxUploadBytes = 500L * 1024 * 1024;
    private const string MdmWebhookSharedSecretHeader = "X-Intune-Secret";

    private readonly IArchivalService _archivalService;
    private readonly IArchivePlanService _archivePlanService;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileBackupController> _logger;

    public MobileBackupController(
        IArchivalService archivalService,
        IArchivePlanService archivePlanService,
        IAuditService auditService,
        IConfiguration configuration,
        ILogger<MobileBackupController> logger)
    {
        _archivalService = archivalService;
        _archivePlanService = archivePlanService;
        _auditService = auditService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(ApiResponse<MobileUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<MobileUploadResponse>>> Upload(
        IFormFile file,
        [FromForm] string deviceId,
        [FromForm] string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequestResponse<MobileUploadResponse>("No file provided.");

        if (file.Length > MaxUploadBytes)
            return BadRequestResponse<MobileUploadResponse>("File exceeds the 500 MB limit.");

        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequestResponse<MobileUploadResponse>("deviceId is required.");

        var tempDir = Path.Combine(Path.GetTempPath(), "edm-mobile-uploads");
        Directory.CreateDirectory(tempDir);

        var safeFileName = Path.GetFileName(file.FileName);
        var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid()}_{safeFileName}");

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var planLabel = label ?? $"mobile-{deviceId}";
            var activePlans = await _archivePlanService.GetActivePlansAsync(cancellationToken);
            var plan = activePlans.FirstOrDefault();

            Guid archiveItemId;
            if (plan != null)
            {
                var job = await _archivalService.CreateJobAsync(plan.Id, Core.Enums.JobPriority.Normal, cancellationToken);
                await _archivalService.StartJobAsync(job.Id, cancellationToken);
                var result = await _archivalService.ArchiveItemAsync(job.Id, tempPath, cancellationToken);
                await _archivalService.CompleteJobAsync(job.Id, cancellationToken);

                archiveItemId = result.Success
                    ? Guid.NewGuid()
                    : Guid.Empty;

                if (!result.Success)
                    return BadRequestResponse<MobileUploadResponse>($"Archive failed: {result.Error}");
            }
            else
            {
                archiveItemId = Guid.NewGuid();
                _logger.LogInformation("No active archive plan found; mobile upload {ArchiveItemId} stored to temp only", archiveItemId);
            }

            return Success(
                new MobileUploadResponse(archiveItemId, "File uploaded successfully."),
                "Upload accepted.");
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [HttpPost("mdm-webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MdmWebhook(
        [FromBody] IntuneWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        var expectedSecret = _configuration["MdmWebhook:SharedSecret"];
        var providedSecret = Request.Headers[MdmWebhookSharedSecretHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(expectedSecret) || providedSecret != expectedSecret)
            return Unauthorized();

        if (payload == null || string.IsNullOrWhiteSpace(payload.DeviceId))
            return BadRequest(new { error = "Invalid payload." });

        if (string.Equals(payload.ComplianceStatus, "noncompliant", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "MDM non-compliance event: DeviceId={DeviceId} UserId={UserId}",
                payload.DeviceId, payload.UserId);

            await _auditService.LogWithDetailsAsync(
                actor: payload.UserId ?? "unknown",
                action: "DeviceNonCompliance",
                details: $"Device {payload.DeviceId} reported non-compliant by Intune.",
                success: false,
                cancellationToken: cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "MDM compliance event: DeviceId={DeviceId} UserId={UserId} Status={Status}",
                payload.DeviceId, payload.UserId, payload.ComplianceStatus);
        }

        return Ok(new { received = true });
    }
}
