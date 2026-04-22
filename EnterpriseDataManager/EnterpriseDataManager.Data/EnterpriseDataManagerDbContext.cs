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
    public DbSet<MfaState> MfaStates => Set<MfaState>();
    public DbSet<StorageProvider> StorageProviders => Set<StorageProvider>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<ScheduledJobRecord> ScheduledJobRecords => Set<ScheduledJobRecord>();
    public DbSet<FilePermission> FilePermissions => Set<FilePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<MfaState>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.StateJson).IsRequired();
            entity.ToTable("MfaStates");
        });

        modelBuilder.Entity<ScheduledJobRecord>(entity =>
        {
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.CronExpression).HasMaxLength(100).IsRequired();
            entity.ToTable("ScheduledJobRecords");
        });

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
