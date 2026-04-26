namespace EnterpriseDataManager.Application.Common.Interfaces;

public interface IReportService
{
    Task<byte[]> GenerateArchiveJobsExcelAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<byte[]> GenerateArchiveJobsPdfAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<byte[]> GenerateAuditLogExcelAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<byte[]> GenerateAuditLogPdfAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
