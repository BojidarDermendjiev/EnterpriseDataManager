namespace EnterpriseDataManager.Data.Repositories;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

public class StorageProviderRepository : GenericRepository<StorageProvider>, IStorageProviderRepository
{
    public StorageProviderRepository(EnterpriseDataManagerDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<StorageProvider>> GetEnabledProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorageProvider>> GetByTypeAsync(
        StorageType type,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.Type == type)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<StorageProvider?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
    }
}
