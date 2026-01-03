namespace EnterpriseDataManager.Data.Repositories;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

public class RetentionPolicyRepository : GenericRepository<RetentionPolicy>, IRetentionPolicyRepository
{
    public RetentionPolicyRepository(EnterpriseDataManagerDbContext context) : base(context)
    {
    }

    public async Task<RetentionPolicy?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<RetentionPolicy>> GetPoliciesWithLegalHoldAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.IsLegalHold)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetentionPolicy>> GetLegalHoldPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetPoliciesWithLegalHoldAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetentionPolicy>> GetImmutablePoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.IsImmutable)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
