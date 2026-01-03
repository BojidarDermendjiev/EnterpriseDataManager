namespace EnterpriseDataManager.Infrastructure.ExternalServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

public class NotificationOptions
{
    public const string SectionName = "Notification";

    public List<string> DefaultAlertRecipients { get; set; } = new();
    public List<WebhookConfiguration> Webhooks { get; set; } = new();
    public bool EnableEmailNotifications { get; set; } = true;
    public bool EnableWebhookNotifications { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan WebhookTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class WebhookConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public List<AlertSeverity> SeverityFilter { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
}

public class NotificationService : INotificationService
{
    private readonly NotificationOptions _options;
    private readonly IEmailService _emailService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationService>? _logger;
    private readonly ConcurrentQueue<NotificationQueueItem> _notificationQueue = new();
    private readonly ConcurrentDictionary<string, NotificationRecord> _notificationHistory = new();

    public NotificationService(
        IOptions<NotificationOptions> options,
        IEmailService emailService,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationService>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _httpClient = httpClientFactory?.CreateClient("Notifications") ?? new HttpClient();
        _httpClient.Timeout = _options.WebhookTimeout;
        _logger = logger;
    }

    public async Task<NotificationResult> SendAlertAsync(
        Alert alert,
        CancellationToken cancellationToken = default)
    {
        var notificationId = Guid.NewGuid().ToString();
        var results = new List<ChannelResult>();

        _logger?.LogInformation("Sending alert: {Title} (Severity: {Severity})", alert.Title, alert.Severity);

        // Send email notifications
        if (_options.EnableEmailNotifications && _options.DefaultAlertRecipients.Count > 0)
        {
            var emailResult = await SendAlertEmailAsync(alert, notificationId, cancellationToken);
            results.Add(emailResult);
        }

        // Send webhook notifications
        if (_options.EnableWebhookNotifications)
        {
            var webhookResults = await SendAlertWebhooksAsync(alert, notificationId, cancellationToken);
            results.AddRange(webhookResults);
        }

        var record = new NotificationRecord(
            NotificationId: notificationId,
            Type: NotificationType.Alert,
            Title: alert.Title,
            SentAt: DateTimeOffset.UtcNow,
            ChannelResults: results,
            Severity: alert.Severity);

        _notificationHistory[notificationId] = record;

        return new NotificationResult(
            Success: results.All(r => r.Success),
            NotificationId: notificationId,
            ChannelResults: results);
    }

    public async Task<NotificationResult> SendAlertAsync(
        AlertSeverity severity,
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        var alert = new Alert(
            Title: title,
            Message: message,
            Severity: severity,
            Source: "EnterpriseDataManager",
            Timestamp: DateTimeOffset.UtcNow,
            Tags: null,
            Metadata: null);

        return await SendAlertAsync(alert, cancellationToken);
    }

    public async Task<NotificationResult> SendWebhookAsync(
        string webhookUrl,
        object payload,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var notificationId = Guid.NewGuid().ToString();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
            request.Content = JsonContent.Create(payload);

            if (headers != null)
            {
                foreach (var (key, value) in headers)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var success = response.IsSuccessStatusCode;

            var channelResult = new ChannelResult(
                Channel: NotificationChannel.Webhook,
                Success: success,
                TargetIdentifier: webhookUrl,
                ErrorMessage: success ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

            var record = new NotificationRecord(
                NotificationId: notificationId,
                Type: NotificationType.Webhook,
                Title: $"Webhook to {webhookUrl}",
                SentAt: DateTimeOffset.UtcNow,
                ChannelResults: new List<ChannelResult> { channelResult },
                Severity: null);

            _notificationHistory[notificationId] = record;

            if (success)
            {
                _logger?.LogInformation("Webhook sent successfully to {Url}", webhookUrl);
            }
            else
            {
                _logger?.LogWarning("Webhook failed to {Url}: {Status}", webhookUrl, response.StatusCode);
            }

            return new NotificationResult(
                Success: success,
                NotificationId: notificationId,
                ChannelResults: new List<ChannelResult> { channelResult });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send webhook to {Url}", webhookUrl);

            var channelResult = new ChannelResult(
                Channel: NotificationChannel.Webhook,
                Success: false,
                TargetIdentifier: webhookUrl,
                ErrorMessage: ex.Message);

            return new NotificationResult(
                Success: false,
                NotificationId: notificationId,
                ChannelResults: new List<ChannelResult> { channelResult });
        }
    }

    public async Task<NotificationResult> SendEmailNotificationAsync(
        IEnumerable<string> recipients,
        string subject,
        string body,
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        var notificationId = Guid.NewGuid().ToString();
        var recipientList = recipients.ToList();

        var emailResult = await _emailService.SendAsync(recipientList, subject, body, isHtml, cancellationToken);

        var channelResult = new ChannelResult(
            Channel: NotificationChannel.Email,
            Success: emailResult.Success,
            TargetIdentifier: string.Join(", ", recipientList),
            ErrorMessage: emailResult.ErrorMessage);

        var record = new NotificationRecord(
            NotificationId: notificationId,
            Type: NotificationType.Email,
            Title: subject,
            SentAt: DateTimeOffset.UtcNow,
            ChannelResults: new List<ChannelResult> { channelResult },
            Severity: null);

        _notificationHistory[notificationId] = record;

        return new NotificationResult(
            Success: emailResult.Success,
            NotificationId: notificationId,
            ChannelResults: new List<ChannelResult> { channelResult });
    }

    public Task<string> QueueNotificationAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        var queueId = Guid.NewGuid().ToString();
        var queueItem = new NotificationQueueItem(
            QueueId: queueId,
            Notification: notification,
            QueuedAt: DateTimeOffset.UtcNow,
            RetryCount: 0);

        _notificationQueue.Enqueue(queueItem);

        _logger?.LogInformation("Notification queued with ID {QueueId}", queueId);

        return Task.FromResult(queueId);
    }

    public async Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        var processed = 0;

        while (_notificationQueue.TryDequeue(out var item) && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNotificationAsync(item.Notification, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to process notification {QueueId}", item.QueueId);

                if (item.RetryCount < _options.MaxRetries)
                {
                    var retryItem = item with { RetryCount = item.RetryCount + 1 };
                    _notificationQueue.Enqueue(retryItem);
                    await Task.Delay(_options.RetryDelay, cancellationToken);
                }
            }
        }

        return processed;
    }

    public Task<NotificationRecord?> GetNotificationAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        _notificationHistory.TryGetValue(notificationId, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<NotificationRecord>> GetRecentNotificationsAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var recent = _notificationHistory.Values
            .OrderByDescending(r => r.SentAt)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<NotificationRecord>>(recent);
    }

    public Task<NotificationStatistics> GetStatisticsAsync(
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var sinceDate = since ?? DateTimeOffset.UtcNow.AddDays(-7);
        var records = _notificationHistory.Values
            .Where(r => r.SentAt >= sinceDate)
            .ToList();

        var stats = new NotificationStatistics(
            TotalNotifications: records.Count,
            SuccessfulNotifications: records.Count(r => r.ChannelResults.All(c => c.Success)),
            FailedNotifications: records.Count(r => r.ChannelResults.Any(c => !c.Success)),
            ByChannel: records
                .SelectMany(r => r.ChannelResults)
                .GroupBy(c => c.Channel)
                .ToDictionary(g => g.Key, g => new ChannelStatistics(
                    Total: g.Count(),
                    Successful: g.Count(c => c.Success),
                    Failed: g.Count(c => !c.Success))),
            BySeverity: records
                .Where(r => r.Severity.HasValue)
                .GroupBy(r => r.Severity!.Value)
                .ToDictionary(g => g.Key, g => g.Count()),
            Since: sinceDate);

        return Task.FromResult(stats);
    }

    private async Task<ChannelResult> SendAlertEmailAsync(
        Alert alert,
        string notificationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = $"[{alert.Severity}] {alert.Title}";
            var body = FormatAlertEmail(alert);

            var result = await _emailService.SendAsync(
                _options.DefaultAlertRecipients,
                subject,
                body,
                isHtml: true,
                cancellationToken);

            return new ChannelResult(
                Channel: NotificationChannel.Email,
                Success: result.Success,
                TargetIdentifier: string.Join(", ", _options.DefaultAlertRecipients),
                ErrorMessage: result.ErrorMessage);
        }
        catch (Exception ex)
        {
            return new ChannelResult(
                Channel: NotificationChannel.Email,
                Success: false,
                TargetIdentifier: string.Join(", ", _options.DefaultAlertRecipients),
                ErrorMessage: ex.Message);
        }
    }

    private async Task<List<ChannelResult>> SendAlertWebhooksAsync(
        Alert alert,
        string notificationId,
        CancellationToken cancellationToken)
    {
        var results = new List<ChannelResult>();

        foreach (var webhook in _options.Webhooks.Where(w => w.IsEnabled))
        {
            // Check severity filter
            if (webhook.SeverityFilter.Count > 0 && !webhook.SeverityFilter.Contains(alert.Severity))
            {
                continue;
            }

            try
            {
                var payload = new WebhookAlertPayload(
                    NotificationId: notificationId,
                    Alert: alert,
                    Timestamp: DateTimeOffset.UtcNow,
                    Source: "EnterpriseDataManager");

                var headers = new Dictionary<string, string>(webhook.Headers);
                if (!string.IsNullOrEmpty(webhook.Secret))
                {
                    var signature = ComputeWebhookSignature(payload, webhook.Secret);
                    headers["X-Webhook-Signature"] = signature;
                }

                var result = await SendWebhookAsync(webhook.Url, payload, headers, cancellationToken);
                results.AddRange(result.ChannelResults);
            }
            catch (Exception ex)
            {
                results.Add(new ChannelResult(
                    Channel: NotificationChannel.Webhook,
                    Success: false,
                    TargetIdentifier: webhook.Url,
                    ErrorMessage: ex.Message));
            }
        }

        return results;
    }

    private async Task ProcessNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        switch (notification)
        {
            case EmailNotification email:
                await SendEmailNotificationAsync(email.Recipients, email.Subject, email.Body, email.IsHtml, cancellationToken);
                break;

            case AlertNotification alert:
                await SendAlertAsync(alert.Alert, cancellationToken);
                break;

            case WebhookNotification webhook:
                await SendWebhookAsync(webhook.Url, webhook.Payload, webhook.Headers, cancellationToken);
                break;
        }
    }

    private static string FormatAlertEmail(Alert alert)
    {
        var severityColor = alert.Severity switch
        {
            AlertSeverity.Critical => "#dc3545",
            AlertSeverity.High => "#fd7e14",
            AlertSeverity.Medium => "#ffc107",
            AlertSeverity.Low => "#17a2b8",
            _ => "#6c757d"
        };

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; }}
        .alert-box {{ border-left: 4px solid {severityColor}; padding: 15px; background-color: #f8f9fa; margin-bottom: 20px; }}
        .severity {{ display: inline-block; padding: 4px 8px; background-color: {severityColor}; color: white; font-weight: bold; border-radius: 4px; }}
        .metadata {{ margin-top: 15px; font-size: 12px; color: #6c757d; }}
    </style>
</head>
<body>
    <div class='alert-box'>
        <p><span class='severity'>{alert.Severity}</span></p>
        <h2>{System.Web.HttpUtility.HtmlEncode(alert.Title)}</h2>
        <p>{System.Web.HttpUtility.HtmlEncode(alert.Message)}</p>
        <div class='metadata'>
            <p><strong>Source:</strong> {System.Web.HttpUtility.HtmlEncode(alert.Source)}</p>
            <p><strong>Time:</strong> {alert.Timestamp:yyyy-MM-dd HH:mm:ss UTC}</p>
            {(alert.Tags != null && alert.Tags.Count > 0 ? $"<p><strong>Tags:</strong> {string.Join(", ", alert.Tags)}</p>" : "")}
        </div>
    </div>
</body>
</html>";
    }

    private static string ComputeWebhookSignature(object payload, string secret)
    {
        var json = JsonSerializer.Serialize(payload);
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var dataBytes = System.Text.Encoding.UTF8.GetBytes(json);

        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public interface INotificationService
{
    Task<NotificationResult> SendAlertAsync(Alert alert, CancellationToken cancellationToken = default);
    Task<NotificationResult> SendAlertAsync(AlertSeverity severity, string title, string message, CancellationToken cancellationToken = default);
    Task<NotificationResult> SendWebhookAsync(string webhookUrl, object payload, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
    Task<NotificationResult> SendEmailNotificationAsync(IEnumerable<string> recipients, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
    Task<string> QueueNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default);
    Task<NotificationRecord?> GetNotificationAsync(string notificationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRecord>> GetRecentNotificationsAsync(int count = 100, CancellationToken cancellationToken = default);
    Task<NotificationStatistics> GetStatisticsAsync(DateTimeOffset? since = null, CancellationToken cancellationToken = default);
}

public record Alert(
    string Title,
    string Message,
    AlertSeverity Severity,
    string Source,
    DateTimeOffset Timestamp,
    IReadOnlyList<string>? Tags,
    Dictionary<string, object>? Metadata);

public record NotificationResult(
    bool Success,
    string NotificationId,
    IReadOnlyList<ChannelResult> ChannelResults);

public record ChannelResult(
    NotificationChannel Channel,
    bool Success,
    string TargetIdentifier,
    string? ErrorMessage);

public record NotificationRecord(
    string NotificationId,
    NotificationType Type,
    string Title,
    DateTimeOffset SentAt,
    IReadOnlyList<ChannelResult> ChannelResults,
    AlertSeverity? Severity);

public record NotificationStatistics(
    int TotalNotifications,
    int SuccessfulNotifications,
    int FailedNotifications,
    Dictionary<NotificationChannel, ChannelStatistics> ByChannel,
    Dictionary<AlertSeverity, int> BySeverity,
    DateTimeOffset Since);

public record ChannelStatistics(
    int Total,
    int Successful,
    int Failed);

public record WebhookAlertPayload(
    string NotificationId,
    Alert Alert,
    DateTimeOffset Timestamp,
    string Source);

public record NotificationQueueItem(
    string QueueId,
    Notification Notification,
    DateTimeOffset QueuedAt,
    int RetryCount);

public abstract record Notification;

public record EmailNotification(
    IReadOnlyList<string> Recipients,
    string Subject,
    string Body,
    bool IsHtml) : Notification;

public record AlertNotification(Alert Alert) : Notification;

public record WebhookNotification(
    string Url,
    object Payload,
    Dictionary<string, string>? Headers) : Notification;

public enum NotificationChannel
{
    Email,
    Webhook,
    Sms,
    Push,
    Slack,
    Teams
}

public enum NotificationType
{
    Alert,
    Email,
    Webhook,
    System
}

public enum AlertSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}
