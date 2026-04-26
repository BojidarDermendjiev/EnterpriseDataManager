namespace EnterpriseDataManager.Controllers.MVC;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EnterpriseDataManager.Core.Interfaces.Repositories;
using EnterpriseDataManager.Core.Enums;

[Authorize]
public sealed class NotificationsController : Controller
{
    private readonly IArchiveJobRepository _archiveJobRepository;

    public NotificationsController(IArchiveJobRepository archiveJobRepository)
    {
        _archiveJobRepository = archiveJobRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken = default)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        var jobs = await _archiveJobRepository.GetJobsInDateRangeAsync(from, to, cancellationToken);

        var notifications = jobs
            .Where(j => j.Status is ArchiveStatus.Completed or ArchiveStatus.Failed or ArchiveStatus.Canceled)
            .OrderByDescending(j => j.CompletedAt ?? j.StartedAt)
            .Take(10)
            .Select(j => new
            {
                id = j.Id,
                type = j.Status switch
                {
                    ArchiveStatus.Completed => "success",
                    ArchiveStatus.Failed => "danger",
                    _ => "warning"
                },
                title = j.Status switch
                {
                    ArchiveStatus.Completed => "Archive Job Completed",
                    ArchiveStatus.Failed => "Archive Job Failed",
                    _ => "Archive Job Canceled"
                },
                body = j.Status == ArchiveStatus.Failed
                    ? $"{j.ArchivePlan?.Name ?? "Unknown plan"}: {j.FailureReason ?? "Unknown error"}"
                    : $"{j.ArchivePlan?.Name ?? "Unknown plan"} finished successfully",
                timeAgo = FormatTimeAgo(j.CompletedAt ?? j.StartedAt ?? j.CreatedAt),
                href = Url.Action("Details", "ArchiveJobs", new { id = j.Id })
            })
            .ToList();

        return Json(notifications);
    }

    private static string FormatTimeAgo(DateTimeOffset time)
    {
        var elapsed = DateTimeOffset.UtcNow - time;
        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
        return time.LocalDateTime.ToString("MMM d");
    }
}
