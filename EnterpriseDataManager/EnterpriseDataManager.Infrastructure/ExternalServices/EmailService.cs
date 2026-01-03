namespace EnterpriseDataManager.Infrastructure.ExternalServices;

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Collections.Concurrent;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpServer { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@enterprise.local";
    public string FromName { get; set; } = "Enterprise Data Manager";
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxConcurrentSends { get; set; } = 10;
}

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService>? _logger;
    private readonly SemaphoreSlim _sendSemaphore;
    private readonly ConcurrentQueue<EmailQueueItem> _emailQueue = new();
    private readonly ConcurrentDictionary<string, EmailDeliveryStatus> _deliveryStatus = new();

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _sendSemaphore = new SemaphoreSlim(_options.MaxConcurrentSends, _options.MaxConcurrentSends);
    }

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var messageId = Guid.NewGuid().ToString();

        try
        {
            await _sendSemaphore.WaitAsync(cancellationToken);

            try
            {
                var mimeMessage = CreateMimeMessage(message);
                mimeMessage.MessageId = messageId;

                await SendWithRetryAsync(mimeMessage, cancellationToken);

                var status = new EmailDeliveryStatus(
                    MessageId: messageId,
                    Status: DeliveryStatus.Sent,
                    SentAt: DateTimeOffset.UtcNow,
                    Recipients: message.To.ToList(),
                    ErrorMessage: null);

                _deliveryStatus[messageId] = status;

                _logger?.LogInformation("Email sent successfully to {Recipients}, MessageId: {MessageId}",
                    string.Join(", ", message.To), messageId);

                return new EmailResult(
                    Success: true,
                    MessageId: messageId,
                    ErrorMessage: null);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            var status = new EmailDeliveryStatus(
                MessageId: messageId,
                Status: DeliveryStatus.Failed,
                SentAt: null,
                Recipients: message.To.ToList(),
                ErrorMessage: ex.Message);

            _deliveryStatus[messageId] = status;

            _logger?.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", message.To));

            return new EmailResult(
                Success: false,
                MessageId: messageId,
                ErrorMessage: ex.Message);
        }
    }

    public async Task<EmailResult> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage(
            To: new[] { to },
            Subject: subject,
            Body: body,
            IsHtml: isHtml,
            Cc: null,
            Bcc: null,
            Attachments: null,
            Priority: EmailPriority.Normal,
            Headers: null);

        return await SendAsync(message, cancellationToken);
    }

    public async Task<EmailResult> SendAsync(
        IEnumerable<string> to,
        string subject,
        string body,
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage(
            To: to.ToList(),
            Subject: subject,
            Body: body,
            IsHtml: isHtml,
            Cc: null,
            Bcc: null,
            Attachments: null,
            Priority: EmailPriority.Normal,
            Headers: null);

        return await SendAsync(message, cancellationToken);
    }

    public Task<string> QueueAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var queueId = Guid.NewGuid().ToString();
        var queueItem = new EmailQueueItem(
            QueueId: queueId,
            Message: message,
            QueuedAt: DateTimeOffset.UtcNow,
            RetryCount: 0);

        _emailQueue.Enqueue(queueItem);

        _logger?.LogInformation("Email queued with ID {QueueId} for {Recipients}",
            queueId, string.Join(", ", message.To));

        return Task.FromResult(queueId);
    }

    public async Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        var processed = 0;

        while (_emailQueue.TryDequeue(out var item) && !cancellationToken.IsCancellationRequested)
        {
            var result = await SendAsync(item.Message, cancellationToken);

            if (!result.Success && item.RetryCount < _options.MaxRetries)
            {
                var retryItem = item with { RetryCount = item.RetryCount + 1 };
                _emailQueue.Enqueue(retryItem);
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }

            processed++;
        }

        return processed;
    }

    public Task<EmailDeliveryStatus?> GetDeliveryStatusAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _deliveryStatus.TryGetValue(messageId, out var status);
        return Task.FromResult(status);
    }

    public Task<IReadOnlyList<EmailDeliveryStatus>> GetRecentDeliveriesAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var recent = _deliveryStatus.Values
            .OrderByDescending(s => s.SentAt ?? DateTimeOffset.MinValue)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<EmailDeliveryStatus>>(recent);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                _options.SmtpServer,
                _options.SmtpPort,
                _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.DisconnectAsync(true, cancellationToken);

            _logger?.LogInformation("SMTP connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SMTP connection test failed");
            return false;
        }
    }

    private MimeMessage CreateMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();

        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));

        foreach (var to in message.To)
        {
            mimeMessage.To.Add(MailboxAddress.Parse(to));
        }

        if (message.Cc != null)
        {
            foreach (var cc in message.Cc)
            {
                mimeMessage.Cc.Add(MailboxAddress.Parse(cc));
            }
        }

        if (message.Bcc != null)
        {
            foreach (var bcc in message.Bcc)
            {
                mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc));
            }
        }

        mimeMessage.Subject = message.Subject;

        mimeMessage.Priority = message.Priority switch
        {
            EmailPriority.High => MessagePriority.Urgent,
            EmailPriority.Low => MessagePriority.NonUrgent,
            _ => MessagePriority.Normal
        };

        if (message.Headers != null)
        {
            foreach (var (key, value) in message.Headers)
            {
                mimeMessage.Headers.Add(key, value);
            }
        }

        var builder = new BodyBuilder();

        if (message.IsHtml)
        {
            builder.HtmlBody = message.Body;
        }
        else
        {
            builder.TextBody = message.Body;
        }

        if (message.Attachments != null)
        {
            foreach (var attachment in message.Attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        mimeMessage.Body = builder.ToMessageBody();

        return mimeMessage;
    }

    private async Task SendWithRetryAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= _options.MaxRetries)
        {
            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(
                    _options.SmtpServer,
                    _options.SmtpPort,
                    _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                    cancellationToken);

                if (!string.IsNullOrEmpty(_options.Username))
                {
                    await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;

                if (retryCount <= _options.MaxRetries)
                {
                    _logger?.LogWarning(ex, "Email send attempt {Attempt} failed, retrying...", retryCount);
                    await Task.Delay(_options.RetryDelay, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException($"Failed to send email after {_options.MaxRetries} retries", lastException);
    }
}

public interface IEmailService
{
    Task<EmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    Task<EmailResult> SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
    Task<EmailResult> SendAsync(IEnumerable<string> to, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
    Task<string> QueueAsync(EmailMessage message, CancellationToken cancellationToken = default);
    Task<int> ProcessQueueAsync(CancellationToken cancellationToken = default);
    Task<EmailDeliveryStatus?> GetDeliveryStatusAsync(string messageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailDeliveryStatus>> GetRecentDeliveriesAsync(int count = 100, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string Body,
    bool IsHtml,
    IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc,
    IReadOnlyList<EmailAttachment>? Attachments,
    EmailPriority Priority,
    Dictionary<string, string>? Headers);

public record EmailAttachment(
    string FileName,
    byte[] Content,
    string ContentType);

public record EmailResult(
    bool Success,
    string MessageId,
    string? ErrorMessage);

public record EmailDeliveryStatus(
    string MessageId,
    DeliveryStatus Status,
    DateTimeOffset? SentAt,
    IReadOnlyList<string> Recipients,
    string? ErrorMessage);

public record EmailQueueItem(
    string QueueId,
    EmailMessage Message,
    DateTimeOffset QueuedAt,
    int RetryCount);

public enum EmailPriority
{
    Low,
    Normal,
    High
}

public enum DeliveryStatus
{
    Queued,
    Sending,
    Sent,
    Failed,
    Bounced
}
