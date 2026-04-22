namespace EnterpriseDataManager.Infrastructure.Services;

using EnterpriseDataManager.Application.Common.Interfaces;
using EnterpriseDataManager.Core.Enums;
using EnterpriseDataManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

public sealed class ReportService : IReportService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<byte[]> GenerateArchiveJobsExcelAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var jobs = await db.ArchiveJobs
            .AsNoTracking()
            .Include(j => j.ArchivePlan)
            .Include(j => j.Items)
            .Where(j => j.CreatedAt >= from && j.CreatedAt <= to)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(ct);

        var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("Archive Jobs");

        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);

        var headerRow = sheet.CreateRow(0);
        string[] headers = { "Job ID", "Plan Name", "Status", "Item Count", "Total Size (Bytes)", "Created At", "Completed At" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
        }

        for (int rowIdx = 0; rowIdx < jobs.Count; rowIdx++)
        {
            var job = jobs[rowIdx];
            var row = sheet.CreateRow(rowIdx + 1);
            row.CreateCell(0).SetCellValue(job.Id.ToString());
            row.CreateCell(1).SetCellValue(job.ArchivePlan?.Name ?? "");
            row.CreateCell(2).SetCellValue(job.Status.ToString());
            row.CreateCell(3).SetCellValue(job.Items.Count);
            row.CreateCell(4).SetCellValue(job.ProcessedBytes);
            row.CreateCell(5).SetCellValue(job.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            row.CreateCell(6).SetCellValue(job.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
        }

        for (int i = 0; i < headers.Length; i++)
            sheet.AutoSizeColumn(i);

        using var ms = new MemoryStream();
        workbook.Write(ms, leaveOpen: true);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateArchiveJobsPdfAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var jobs = await db.ArchiveJobs
            .AsNoTracking()
            .Include(j => j.ArchivePlan)
            .Where(j => j.CreatedAt >= from && j.CreatedAt <= to)
            .OrderByDescending(j => j.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var document = new PdfDocument();
        document.Info.Title = "Archive Jobs Report";

        var page = document.AddPage();
        page.Orientation = PdfSharpCore.PageOrientation.Landscape;
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 9, XFontStyle.Bold);
        var cellFont = new XFont("Arial", 8, XFontStyle.Regular);

        gfx.DrawString($"Archive Jobs Report ({from:yyyy-MM-dd} to {to:yyyy-MM-dd})", titleFont, XBrushes.Black, new XRect(20, 20, page.Width - 40, 20), XStringFormats.TopLeft);
        gfx.DrawString($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC", cellFont, XBrushes.Gray, new XRect(20, 38, page.Width - 40, 14), XStringFormats.TopLeft);

        double[] colWidths = { 210, 120, 70, 70, 100, 115, 115 };
        string[] headers = { "Job ID", "Plan Name", "Status", "Items", "Size (Bytes)", "Created At", "Completed At" };

        double tableLeft = 20;
        double rowHeight = 14;
        double y = 60;

        gfx.DrawRectangle(XBrushes.DarkSlateGray, new XRect(tableLeft, y, colWidths.Sum(), rowHeight));
        double x = tableLeft;
        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], headerFont, XBrushes.White, new XRect(x + 2, y + 2, colWidths[i] - 4, rowHeight - 2), XStringFormats.TopLeft);
            x += colWidths[i];
        }
        y += rowHeight;

        for (int rowIdx = 0; rowIdx < jobs.Count; rowIdx++)
        {
            if (y + rowHeight > page.Height - 20)
            {
                page = document.AddPage();
                page.Orientation = PdfSharpCore.PageOrientation.Landscape;
                gfx = XGraphics.FromPdfPage(page);
                y = 20;
            }

            var job = jobs[rowIdx];
            var bg = rowIdx % 2 == 0 ? XBrushes.White : new XSolidBrush(XColor.FromArgb(240, 240, 245));
            gfx.DrawRectangle(bg, new XRect(tableLeft, y, colWidths.Sum(), rowHeight));

            string[] values =
            {
                job.Id.ToString(),
                Truncate(job.ArchivePlan?.Name ?? "", 22),
                job.Status.ToString(),
                job.TotalItemCount.ToString(),
                job.ProcessedBytes.ToString(),
                job.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                job.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? ""
            };

            x = tableLeft;
            for (int i = 0; i < values.Length; i++)
            {
                gfx.DrawString(values[i], cellFont, XBrushes.Black, new XRect(x + 2, y + 2, colWidths[i] - 4, rowHeight - 2), XStringFormats.TopLeft);
                x += colWidths[i];
            }

            gfx.DrawLine(XPens.LightGray, tableLeft, y + rowHeight, tableLeft + colWidths.Sum(), y + rowHeight);
            y += rowHeight;
        }

        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateAuditLogExcelAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseDataManagerDbContext>();

        var records = await db.AuditRecords
            .AsNoTracking()
            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(ct);

        var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("Audit Log");

        var headerStyle = workbook.CreateCellStyle();
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerStyle.SetFont(headerFont);

        var headerRow = sheet.CreateRow(0);
        string[] headers = { "Timestamp", "Actor", "Action", "Resource Type", "Resource ID", "Success", "Details", "IP Address" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = headerRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
        }

        for (int rowIdx = 0; rowIdx < records.Count; rowIdx++)
        {
            var record = records[rowIdx];
            var row = sheet.CreateRow(rowIdx + 1);
            row.CreateCell(0).SetCellValue(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            row.CreateCell(1).SetCellValue(record.Actor);
            row.CreateCell(2).SetCellValue(record.Action);
            row.CreateCell(3).SetCellValue(record.ResourceType ?? "");
            row.CreateCell(4).SetCellValue(record.ResourceId ?? "");
            row.CreateCell(5).SetCellValue(record.Success);
            row.CreateCell(6).SetCellValue(record.Details ?? "");
            row.CreateCell(7).SetCellValue(record.IpAddress ?? "");
        }

        for (int i = 0; i < headers.Length; i++)
            sheet.AutoSizeColumn(i);

        using var ms = new MemoryStream();
        workbook.Write(ms, leaveOpen: true);
        return ms.ToArray();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";
}
