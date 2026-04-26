namespace EnterpriseDataManager.IntegrationTests.Archive;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Data;
using EnterpriseDataManager.IntegrationTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class ArchiveRecoverVerifyTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly List<string> _tempDirectories = new();

    public ArchiveRecoverVerifyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullFlow_ArchiveSourceFiles_RecoverToDestination_IntegrityVerified()
    {
        // Arrange — create temp directories and source files
        var sourceDir = CreateTempDirectory();
        var archiveStorageDir = CreateTempDirectory();
        var recoveryDestDir = CreateTempDirectory();

        var fileContents = new Dictionary<string, string>
        {
            ["file1.txt"] = "Hello from file 1 — enterprise data content",
            ["file2.txt"] = "Second file with different content for integrity testing",
            ["file3.txt"] = "Third file: 1234567890 special chars: !@#$%"
        };

        foreach (var (name, content) in fileContents)
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, name), content);
        }

        Guid planId;
        Guid jobId;

        // Seed StorageProvider and ArchivePlan
        await _factory.SeedAsync(async db =>
        {
            var provider = StorageProvider.CreateLocal("TestLocalProvider", archiveStorageDir);
            db.StorageProviders.Add(provider);
            await db.SaveChangesAsync();

            var plan = ArchivePlan.Create("TestPlan", sourceDir);
            plan.SetStorageProvider(provider);
            plan.Activate();
            db.ArchivePlans.Add(plan);
            await db.SaveChangesAsync();

            planId = plan.Id;
        });

        // Re-query the plan ID since the lambda capture may not be reliable across async boundaries
        using var seedScope = _factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        var seededPlan = await seedDb.ArchivePlans.FirstAsync();
        planId = seededPlan.Id;

        // Act — archive flow
        using var archiveScope = _factory.Services.CreateScope();
        var archivalService = archiveScope.ServiceProvider.GetRequiredService<IArchivalService>();

        var job = await archivalService.CreateJobAsync(planId);
        jobId = job.Id;

        await archivalService.StartJobAsync(jobId);
        await archivalService.ExecuteArchiveJobAsync(jobId);

        // Assert — archive completed
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var completedJob = await assertDb.ArchiveJobs
            .Include(j => j.Items)
            .FirstAsync(j => j.Id == jobId);

        completedJob.Status.Should().Be(ArchiveStatus.Completed);

        var items = completedJob.Items.ToList();
        items.Should().HaveCount(3);
        items.Should().AllSatisfy(item =>
        {
            item.Hash.Should().NotBeNullOrEmpty();
            item.TargetPath.Should().NotBeNullOrEmpty();
        });

        // Assert archived files physically exist
        foreach (var item in items)
        {
            var physicalPath = Path.Combine(archiveStorageDir, item.TargetPath!);
            File.Exists(physicalPath).Should().BeTrue(
                $"archived file should exist at {physicalPath}");
        }

        // Act — recovery flow
        var archiveItemPaths = items.Select(i => i.TargetPath!).ToList();
        var recoveryDestPath = recoveryDestDir;

        using var recoveryScope = _factory.Services.CreateScope();
        var recoveryService = recoveryScope.ServiceProvider.GetRequiredService<IRecoveryService>();

        var recoveryJob = await recoveryService.CreateRecoveryJobAsync(jobId, recoveryDestPath);
        await recoveryService.StartRecoveryAsync(recoveryJob.Id);
        var recoveryResults = await recoveryService.RecoverItemsAsync(recoveryJob.Id, archiveItemPaths);
        await recoveryService.CompleteRecoveryAsync(recoveryJob.Id);

        // Assert — recovery completed
        using var recoveryAssertScope = _factory.Services.CreateScope();
        var recoveryAssertDb = recoveryAssertScope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        var completedRecovery = await recoveryAssertDb.RecoveryJobs.FirstAsync(r => r.Id == recoveryJob.Id);
        completedRecovery.Status.Should().Be(ArchiveStatus.Completed);

        // Assert recovered files exist and content matches
        foreach (var (name, expectedContent) in fileContents)
        {
            var recoveredPath = Path.Combine(recoveryDestPath, name);
            File.Exists(recoveredPath).Should().BeTrue($"recovered file '{name}' should exist");
            var actualContent = await File.ReadAllTextAsync(recoveredPath);
            actualContent.Should().Be(expectedContent, $"content of '{name}' should match original byte-for-byte");
        }

        // Assert all recovery results succeeded
        recoveryResults.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        // Assert integrity validation passes
        using var integrityScope = _factory.Services.CreateScope();
        var integrityRecoveryService = integrityScope.ServiceProvider.GetRequiredService<IRecoveryService>();
        var integrityResult = await integrityRecoveryService.ValidateArchiveIntegrityAsync(jobId);
        integrityResult.Should().BeTrue("all archived items should have matching hashes");
    }

    [Fact]
    public async Task ArchiveJob_EmptySourceDirectory_CompletesWithZeroItems()
    {
        // Arrange
        var emptySourceDir = CreateTempDirectory();
        var archiveStorageDir = CreateTempDirectory();

        await _factory.SeedAsync(async db =>
        {
            var provider = StorageProvider.CreateLocal("EmptyTestProvider", archiveStorageDir);
            db.StorageProviders.Add(provider);
            await db.SaveChangesAsync();

            var plan = ArchivePlan.Create("EmptyPlan", emptySourceDir);
            plan.SetStorageProvider(provider);
            plan.Activate();
            db.ArchivePlans.Add(plan);
            await db.SaveChangesAsync();
        });

        using var seedScope = _factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        var plan = await seedDb.ArchivePlans
            .Where(p => p.Name == "EmptyPlan")
            .FirstAsync();

        // Act
        using var scope = _factory.Services.CreateScope();
        var archivalService = scope.ServiceProvider.GetRequiredService<IArchivalService>();

        var job = await archivalService.CreateJobAsync(plan.Id);
        await archivalService.StartJobAsync(job.Id);
        await archivalService.ExecuteArchiveJobAsync(job.Id);

        // Assert
        using var assertScope = _factory.Services.CreateScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        var completedJob = await db2.ArchiveJobs
            .Include(j => j.Items)
            .FirstAsync(j => j.Id == job.Id);

        completedJob.Status.Should().Be(ArchiveStatus.Completed,
            "an empty source directory should complete without failure");
        completedJob.Items.Should().BeEmpty("no files were present to archive");
    }

    [Fact]
    public async Task Recovery_MissingArchiveFile_MarksItemFailed()
    {
        // Arrange
        var sourceDir = CreateTempDirectory();
        var archiveStorageDir = CreateTempDirectory();
        var recoveryDestDir = CreateTempDirectory();

        await File.WriteAllTextAsync(Path.Combine(sourceDir, "file_a.txt"), "Content A");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "file_b.txt"), "Content B");

        await _factory.SeedAsync(async db =>
        {
            var provider = StorageProvider.CreateLocal("MissingFileProvider", archiveStorageDir);
            db.StorageProviders.Add(provider);
            await db.SaveChangesAsync();

            var plan = ArchivePlan.Create("MissingFilePlan", sourceDir);
            plan.SetStorageProvider(provider);
            plan.Activate();
            db.ArchivePlans.Add(plan);
            await db.SaveChangesAsync();
        });

        using var seedScope = _factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        var plan = await seedDb.ArchivePlans
            .Where(p => p.Name == "MissingFilePlan")
            .FirstAsync();

        // Archive both files
        using var archiveScope = _factory.Services.CreateScope();
        var archivalService = archiveScope.ServiceProvider.GetRequiredService<IArchivalService>();

        var job = await archivalService.CreateJobAsync(plan.Id);
        await archivalService.StartJobAsync(job.Id);
        await archivalService.ExecuteArchiveJobAsync(job.Id);

        // Retrieve archive items
        using var itemScope = _factory.Services.CreateScope();
        var itemDb = itemScope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();
        var archivedJob = await itemDb.ArchiveJobs
            .Include(j => j.Items)
            .FirstAsync(j => j.Id == job.Id);

        archivedJob.Status.Should().Be(ArchiveStatus.Completed);
        archivedJob.Items.Should().HaveCount(2);

        // Delete one archived file from storage to simulate corruption/missing file
        var firstItem = archivedJob.Items.First();
        var physicalPath = Path.Combine(archiveStorageDir, firstItem.TargetPath!);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        // Attempt recovery of both items
        var archiveItemPaths = archivedJob.Items.Select(i => i.TargetPath!).ToList();

        using var recoveryScope = _factory.Services.CreateScope();
        var recoveryService = recoveryScope.ServiceProvider.GetRequiredService<IRecoveryService>();

        var recoveryJob = await recoveryService.CreateRecoveryJobAsync(job.Id, recoveryDestDir);
        await recoveryService.StartRecoveryAsync(recoveryJob.Id);
        var results = await recoveryService.RecoverItemsAsync(recoveryJob.Id, archiveItemPaths);

        // Assert: 1 success and 1 failure
        results.Should().HaveCount(2);
        results.Count(r => r.Success).Should().Be(1, "one file was intact and should recover");
        results.Count(r => !r.Success).Should().Be(1, "one file was deleted and should fail");

        var failedResult = results.First(r => !r.Success);
        failedResult.Error.Should().NotBeNullOrEmpty("a failure reason should be provided");
    }

    private string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"edm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirectories.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup
            }
        }
    }
}
