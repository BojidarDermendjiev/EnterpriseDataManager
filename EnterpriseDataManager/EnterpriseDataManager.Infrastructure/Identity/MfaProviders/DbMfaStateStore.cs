namespace EnterpriseDataManager.Infrastructure.Identity.MfaProviders;

using EnterpriseDataManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

public class DbMfaStateStore : IMfaStateStore
{
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DbMfaStateStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task<MfaUserState?> GetStateAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var record = await db.MfaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (record is null)
            return null;

        return JsonSerializer.Deserialize<MfaUserState>(record.StateJson, _json);
    }

    public async Task SaveStateAsync(MfaUserState state, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var json = JsonSerializer.Serialize(state, _json);

        var existing = await db.MfaStates
            .FirstOrDefaultAsync(s => s.UserId == state.UserId, cancellationToken);

        if (existing is null)
        {
            db.MfaStates.Add(new MfaState
            {
                UserId = state.UserId,
                StateJson = json,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.StateJson = json;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteStateAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var record = await db.MfaStates
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (record is null)
            return false;

        db.MfaStates.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
