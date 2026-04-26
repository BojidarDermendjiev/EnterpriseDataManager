namespace EnterpriseDataManager.IntegrationTests.Data;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

[Collection("PostgreSql")]
public class PostgreSqlMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("edm_test")
        .WithUsername("edm_user")
        .WithPassword("edm_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    private DbContextOptions<EnterpriseDataManagerDbContext> BuildOptions()
    {
        return new DbContextOptionsBuilder<EnterpriseDataManagerDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                b => b.MigrationsAssembly("EnterpriseDataManager.Data"))
            .Options;
    }

    [Fact]
    public async Task AllMigrations_ApplyCleanly_ToPostgreSQL()
    {
        // Arrange
        var options = BuildOptions();

        // Act
        await using var db = new EnterpriseDataManagerDbContext(options);
        var exception = await Record.ExceptionAsync(() => db.Database.MigrateAsync());

        // Assert — migration should complete without error
        exception.Should().BeNull("all migrations must apply cleanly to a fresh PostgreSQL database");

        // Verify key tables exist in information_schema
        var expectedTables = new[]
        {
            "ArchivePlans", "ArchiveJobs", "ArchiveItems",
            "RecoveryJobs", "RetentionPolicies", "StorageProviders",
            "AuditRecords", "FilePermissions", "RefreshTokens", "MfaStates",
            "ScheduledJobRecords",
            "AspNetUsers", "AspNetRoles"
        };

        foreach (var tableName in expectedTables)
        {
            var count = await db.Database.ExecuteSqlRawAsync(
                $"SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName.ToLowerInvariant()}'");

            // ExecuteSqlRaw returns rows affected (0 for SELECT); use raw query instead
            var exists = await db.Database
                .SqlQueryRaw<int>(
                    $"SELECT COUNT(*)::int FROM information_schema.tables WHERE table_schema = 'public' AND LOWER(table_name) = LOWER('{tableName}')")
                .FirstOrDefaultAsync();

            exists.Should().Be(1, $"table '{tableName}' should exist after running all migrations");
        }
    }

    [Fact]
    public async Task PostgreSQL_Schema_SupportsAllEntityOperations()
    {
        // Arrange
        var options = BuildOptions();
        await using var db = new EnterpriseDataManagerDbContext(options);
        await db.Database.MigrateAsync();

        // Act — insert a StorageProvider
        var provider = StorageProvider.CreateLocal("PgSqlTestProvider", "/tmp/pg-test-storage");
        db.StorageProviders.Add(provider);
        await db.SaveChangesAsync();

        // Act — insert an ArchivePlan linked to the provider
        var plan = ArchivePlan.Create("PgSqlTestPlan", "/tmp/pg-test-source");
        plan.SetStorageProvider(provider);
        plan.Activate();
        db.ArchivePlans.Add(plan);
        await db.SaveChangesAsync();

        // Act — query back using fresh context to avoid first-level cache
        await using var readDb = new EnterpriseDataManagerDbContext(options);

        var queriedProvider = await readDb.StorageProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == provider.Id);

        var queriedPlan = await readDb.ArchivePlans
            .AsNoTracking()
            .Include(p => p.StorageProvider)
            .FirstOrDefaultAsync(p => p.Id == plan.Id);

        // Assert — round-trip correctness
        queriedProvider.Should().NotBeNull("the inserted StorageProvider must be retrievable");
        queriedProvider!.Name.Should().Be("PgSqlTestProvider");
        queriedProvider.Type.Should().Be(StorageType.Local);
        queriedProvider.RootPath.Should().Be("/tmp/pg-test-storage");

        queriedPlan.Should().NotBeNull("the inserted ArchivePlan must be retrievable");
        queriedPlan!.Name.Should().Be("PgSqlTestPlan");
        queriedPlan.IsActive.Should().BeTrue();
        queriedPlan.StorageProvider.Should().NotBeNull();
        queriedPlan.StorageProvider!.Id.Should().Be(provider.Id);
    }

    [Fact]
    public async Task IdempotentMigration_AppliedTwice_DoesNotFail()
    {
        // Arrange
        var options = BuildOptions();
        await using var db = new EnterpriseDataManagerDbContext(options);

        // Act — apply migrations the first time
        await db.Database.MigrateAsync();

        // Act — apply migrations a second time; EF tracks applied migrations internally
        var exception = await Record.ExceptionAsync(() => db.Database.MigrateAsync());

        // Assert
        exception.Should().BeNull(
            "calling MigrateAsync on an already-migrated database must be idempotent");
    }
}
