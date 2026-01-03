namespace EnterpriseDataManager.Data;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Entities.Common;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

public class EnterpriseDataManagerDbContext : IdentityDbContext
{
    public EnterpriseDataManagerDbContext(DbContextOptions<EnterpriseDataManagerDbContext> options)
        : base(options)
    {
    }

    public DbSet<ArchivePlan> ArchivePlans => Set<ArchivePlan>();
    public DbSet<ArchiveJob> ArchiveJobs => Set<ArchiveJob>();
    public DbSet<ArchiveItem> ArchiveItems => Set<ArchiveItem>();
    public DbSet<RecoveryJob> RecoveryJobs => Set<RecoveryJob>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<StorageProvider> StorageProviders => Set<StorageProvider>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyGlobalQueryFilters(modelBuilder);
    }

    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(EnterpriseDataManagerDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, new object[] { modelBuilder });
            }
        }
    }

    private static void ApplySoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, ISoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(null, cancellationToken);
    }

    public Task<int> SaveChangesAsync(string? userId, CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(userId);
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields(null);
        return base.SaveChanges();
    }

    private void UpdateAuditFields(string? userId)
    {
        var entries = ChangeTracker.Entries<IAuditable>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreated(userId);
                    break;
                case EntityState.Modified:
                    entry.Entity.SetUpdated(userId);
                    break;
            }
        }
    }
}
