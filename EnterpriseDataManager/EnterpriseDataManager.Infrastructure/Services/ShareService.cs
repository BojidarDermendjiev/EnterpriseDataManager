namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Data;
using EnterpriseDataManager.Infrastructure.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class ShareService : IShareService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ShareService>? _logger;

    public ShareService(
        IServiceScopeFactory scopeFactory,
        IEncryptionService encryptionService,
        ILogger<ShareService>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<FilePermission> GrantPermissionAsync(
        string archiveItemId,
        string userId,
        PermissionLevel level,
        DateTimeOffset? expiresAt,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var existing = await db.FilePermissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.ArchiveItemId == archiveItemId && p.UserId == userId, ct);

        if (existing != null)
        {
            existing.Level = level;
            existing.ExpiresAt = expiresAt;
            existing.Restore();
        }
        else
        {
            existing = new FilePermission
            {
                ArchiveItemId = archiveItemId,
                UserId = userId,
                Level = level,
                ExpiresAt = expiresAt
            };
            db.FilePermissions.Add(existing);
        }

        await db.SaveChangesAsync(ct);
        _logger?.LogInformation("Granted {Level} permission on {ArchiveItemId} to {UserId}", level, archiveItemId, userId);
        return existing;
    }

    public async Task RevokePermissionAsync(string archiveItemId, string userId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var permission = await db.FilePermissions
            .FirstOrDefaultAsync(p => p.ArchiveItemId == archiveItemId && p.UserId == userId, ct);

        if (permission != null)
        {
            db.FilePermissions.Remove(permission);
            await db.SaveChangesAsync(ct);
            _logger?.LogInformation("Revoked permission on {ArchiveItemId} for {UserId}", archiveItemId, userId);
        }
    }

    public async Task<IReadOnlyList<FilePermission>> GetPermissionsAsync(string archiveItemId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        return await db.FilePermissions
            .Where(p => p.ArchiveItemId == archiveItemId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<string> GenerateSignedUrlAsync(
        string archiveItemId,
        string userId,
        TimeSpan validity,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var expiresAt = DateTimeOffset.UtcNow.Add(validity);
        var plaintext = $"{archiveItemId}:{userId}:{expiresAt:O}";
        var token = await _encryptionService.EncryptStringAsync(plaintext, ct);

        var permission = await db.FilePermissions
            .FirstOrDefaultAsync(p => p.ArchiveItemId == archiveItemId && p.UserId == userId, ct);

        if (permission == null)
        {
            permission = new FilePermission
            {
                ArchiveItemId = archiveItemId,
                UserId = userId,
                Level = PermissionLevel.Read,
                SignedUrlToken = token,
                SignedUrlExpiresAt = expiresAt
            };
            db.FilePermissions.Add(permission);
        }
        else
        {
            permission.SignedUrlToken = token;
            permission.SignedUrlExpiresAt = expiresAt;
        }

        await db.SaveChangesAsync(ct);
        _logger?.LogInformation("Generated signed URL for {ArchiveItemId} / {UserId} expiring at {ExpiresAt}", archiveItemId, userId, expiresAt);
        return token;
    }

    public async Task<bool> ValidateSignedUrlAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var plaintext = await _encryptionService.DecryptStringAsync(token, ct);
            var parts = plaintext.Split(':');
            if (parts.Length < 3)
                return false;

            if (!DateTimeOffset.TryParse(string.Join(":", parts[2..]), out var expiresAt))
                return false;

            if (expiresAt <= DateTimeOffset.UtcNow)
                return false;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

            return await db.FilePermissions
                .AnyAsync(p => p.SignedUrlToken == token && p.SignedUrlExpiresAt > DateTimeOffset.UtcNow, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Signed URL validation failed");
            return false;
        }
    }
}
