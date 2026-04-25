namespace EnterpriseDataManager.Controllers.Api;

using Asp.Versioning;
using EnterpriseDataManager.Application.DTOs.Mobile;
using EnterpriseDataManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;
using System.Text;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mobile")]
public class MobileBackupController : ApiBaseController
{
    private const long MaxUploadBytes = 500L * 1024 * 1024;
    private const int WebhookTimestampToleranceSeconds = 300;

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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
            try
            {
                await using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                await file.CopyToAsync(stream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write temp file for mobile upload from device {DeviceId}", deviceId);
                return BadRequestResponse<MobileUploadResponse>("Failed to process upload.");
            }

            var activePlans = await _archivePlanService.GetActivePlansAsync(cancellationToken);
            var plan = activePlans.FirstOrDefault();

            if (plan == null)
            {
                try
                {
                    if (System.IO.File.Exists(tempPath))
                        System.IO.File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp file {TempPath} after no-plan early return", tempPath);
                }

                return Conflict(new { error = "No active archive plan found. Create and activate an archive plan before uploading.", code = "NO_ACTIVE_PLAN" });
            }

            var job = await _archivalService.CreateJobAsync(plan.Id, Core.Enums.JobPriority.Normal, cancellationToken);
            await _archivalService.StartJobAsync(job.Id, cancellationToken);
            var result = await _archivalService.ArchiveItemAsync(job.Id, tempPath, cancellationToken);
            await _archivalService.CompleteJobAsync(job.Id, cancellationToken);

            if (!result.Success)
                return BadRequestResponse<MobileUploadResponse>($"Archive failed: {result.Error}");

            return Success(
                new MobileUploadResponse(Guid.NewGuid(), "File uploaded successfully."),
                "Upload accepted.");
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp file {TempPath}", tempPath);
            }
        }
    }

    [HttpPost("mdm-webhook")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MdmWebhook(CancellationToken cancellationToken = default)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        Request.EnableBuffering();
        byte[] bodyBytes;
        using (var ms = new MemoryStream())
        {
            await Request.Body.CopyToAsync(ms, cancellationToken);
            bodyBytes = ms.ToArray();
        }
        Request.Body.Position = 0;

        var timestampHeader = Request.Headers["X-Webhook-Timestamp"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(timestampHeader) || !long.TryParse(timestampHeader, out var timestampUnix))
        {
            _logger.LogWarning("MDM webhook rejected: missing or invalid X-Webhook-Timestamp from {RemoteIp}", remoteIp);
            return Unauthorized();
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
        if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalSeconds) > WebhookTimestampToleranceSeconds)
        {
            _logger.LogWarning(
                "MDM webhook rejected: timestamp {Timestamp} outside {Window}s window from {RemoteIp}",
                timestampUnix, WebhookTimestampToleranceSeconds, remoteIp);
            return Unauthorized();
        }

        var webhookSecret = _configuration["WebhookSettings:Secret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogWarning("MDM webhook rejected: WebhookSettings:Secret is not configured. Request from {RemoteIp}", remoteIp);
            return Unauthorized();
        }

        var signatureHeader = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("MDM webhook rejected: missing or malformed X-Hub-Signature-256 from {RemoteIp}", remoteIp);
            return Unauthorized();
        }

        var providedHex = signatureHeader["sha256=".Length..];
        var secretBytes = Encoding.UTF8.GetBytes(webhookSecret);
        var computedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        var providedBytes = Convert.FromHexString(providedHex.Length % 2 == 0 ? providedHex : string.Empty);
        var computedBytes = Convert.FromHexString(computedHex);

        if (providedBytes.Length == 0 || !CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes))
        {
            _logger.LogWarning("MDM webhook rejected: HMAC signature mismatch from {RemoteIp}", remoteIp);
            return Unauthorized();
        }

        IntuneWebhookPayload? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<IntuneWebhookPayload>(
                bodyBytes,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MDM webhook rejected: failed to deserialize payload from {RemoteIp}", remoteIp);
            return BadRequest(new { error = "Invalid payload." });
        }

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
