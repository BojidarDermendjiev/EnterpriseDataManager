namespace EnterpriseDataManager.Core.Entities;

using EnterpriseDataManager.Common;
using EnterpriseDataManager.Core.Entities.Common;
using static EnterpriseDataManager.Common.EntityValidationConstants;
public class ArchiveItem : BaseEntity
{
    public Guid ArchiveJobId { get; private set; }
    public ArchiveJob? ArchiveJob { get; private set; }
    public string SourcePath { get; private set; } = default!;
    public string TargetPath { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string? Hash { get; private set; }
    public bool? Success { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    protected ArchiveItem() { }

    private ArchiveItem(ArchiveJob job, string sourcePath, string targetPath, long sizeBytes)
    {
        ArchiveJobId = job.Id;
        ArchiveJob = job;
        SourcePath = sourcePath;
        TargetPath = targetPath;
        SizeBytes = sizeBytes;
    }

    internal static ArchiveItem Create(ArchiveJob job, string sourcePath, string targetPath, long sizeBytes)
    {
        Guard.AgainstNullOrWhiteSpace(sourcePath, SourcePathCannotBeEmpty);
        Guard.AgainstNullOrWhiteSpace(targetPath, TargetPathCannotBeEmpty);
        Guard.AgainstNegative(sizeBytes, SizeCannotBeNegative);

        return new ArchiveItem(job, sourcePath.Trim(), targetPath.Trim(), sizeBytes);
    }

    internal void MarkSuccess(string? hash = null)
    {
        Success = true;
        Hash = hash;
        Error = null;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    internal void MarkFailed(string error)
    {
        Guard.AgainstNullOrWhiteSpace(error, ErrorMessageCannotBeEmpty);

        Success = false;
        Error = error;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public bool IsPending => Success is null;
    public bool IsProcessed => Success is not null;
}
