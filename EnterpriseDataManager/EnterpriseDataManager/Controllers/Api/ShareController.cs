namespace EnterpriseDataManager.Controllers.Api;

using Asp.Versioning;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/share")]
public class ShareController : ApiBaseController
{
    private readonly IShareService _shareService;
    private readonly ILogger<ShareController> _logger;

    public ShareController(IShareService shareService, ILogger<ShareController> logger)
    {
        _shareService = shareService;
        _logger = logger;
    }

    [HttpPost("{archiveItemId}/grant")]
    [ProducesResponseType(typeof(ApiResponse<FilePermission>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<FilePermission>>> Grant(
        string archiveItemId,
        [FromBody] GrantPermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequestResponse<FilePermission>("userId is required.");

        var permission = await _shareService.GrantPermissionAsync(
            archiveItemId,
            request.UserId,
            request.Level,
            request.ExpiresAt,
            cancellationToken);

        return Success(permission);
    }

    [HttpDelete("{archiveItemId}/revoke/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(
        string archiveItemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await _shareService.RevokePermissionAsync(archiveItemId, userId, cancellationToken);
        return Success<object>(null!, "Permission revoked.");
    }

    [HttpGet("{archiveItemId}/permissions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FilePermission>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FilePermission>>>> GetPermissions(
        string archiveItemId,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _shareService.GetPermissionsAsync(archiveItemId, cancellationToken);
        return Success(permissions);
    }

    [HttpPost("{archiveItemId}/signed-url")]
    [ProducesResponseType(typeof(ApiResponse<SignedUrlResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SignedUrlResponse>>> GenerateSignedUrl(
        string archiveItemId,
        [FromBody] SignedUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequestResponse<SignedUrlResponse>("userId is required.");

        if (request.ValidityMinutes <= 0)
            return BadRequestResponse<SignedUrlResponse>("validityMinutes must be greater than zero.");

        var validity = TimeSpan.FromMinutes(request.ValidityMinutes);
        var token = await _shareService.GenerateSignedUrlAsync(archiveItemId, request.UserId, validity, cancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(validity);

        return Success(new SignedUrlResponse(token, expiresAt));
    }

    [HttpGet("validate")]
    [ProducesResponseType(typeof(ApiResponse<TokenValidationResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TokenValidationResult>>> Validate(
        [FromQuery] string token,
        CancellationToken cancellationToken = default)
    {
        var valid = await _shareService.ValidateSignedUrlAsync(token, cancellationToken);
        return Success(new TokenValidationResult(valid));
    }
}

public record GrantPermissionRequest(string UserId, PermissionLevel Level, DateTimeOffset? ExpiresAt);
public record SignedUrlRequest(string UserId, int ValidityMinutes);
public record SignedUrlResponse(string Token, DateTimeOffset ExpiresAt);
public record TokenValidationResult(bool Valid);
