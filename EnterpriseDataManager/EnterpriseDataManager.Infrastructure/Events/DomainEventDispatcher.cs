namespace EnterpriseDataManager.Infrastructure.Events;

using EnterpriseDataManager.Core.Entities.Common;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Infrastructure.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator? _mediator;
    private readonly ILogger<DomainEventDispatcher>? _logger;
    private readonly ISiemForwarder? _siemForwarder;
    private readonly DomainEventDispatcherOptions _options;
    private readonly ConcurrentQueue<DomainEventRecord> _eventHistory = new();

    public DomainEventDispatcher(
        IServiceProvider serviceProvider,
        IMediator? mediator = null,
        ISiemForwarder? siemForwarder = null,
        ILogger<DomainEventDispatcher>? logger = null,
        DomainEventDispatcherOptions? options = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _mediator = mediator;
        _siemForwarder = siemForwarder;
        _logger = logger;
        _options = options ?? new DomainEventDispatcherOptions();
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventType = domainEvent.GetType();
        var eventName = eventType.Name;

        _logger?.LogDebug("Dispatching domain event {EventType}", eventName);

        var record = new DomainEventRecord(
            EventId: Guid.NewGuid().ToString(),
            EventType: eventName,
            OccurredAt: domainEvent.OccurredAt,
            DispatchedAt: DateTimeOffset.UtcNow,
            Success: false,
            ErrorMessage: null);

        try
        {
            // Try MediatR first if available
            if (_mediator != null && _options.UseMediator)
            {
                await DispatchViaMediatorAsync(domainEvent, cancellationToken);
            }

            // Then dispatch to registered handlers
            await DispatchToHandlersAsync(domainEvent, eventType, cancellationToken);

            // Forward to SIEM if configured
            if (_options.ForwardToSiem && _siemForwarder != null)
            {
                await ForwardToSiemAsync(domainEvent, cancellationToken);
            }

            record = record with { Success = true };
            _logger?.LogInformation("Successfully dispatched domain event {EventType}", eventName);
        }
        catch (Exception ex)
        {
            record = record with { Success = false, ErrorMessage = ex.Message };
            _logger?.LogError(ex, "Failed to dispatch domain event {EventType}", eventName);

            if (_options.ThrowOnError)
            {
                throw;
            }
        }
        finally
        {
            _eventHistory.Enqueue(record);

            while (_eventHistory.Count > _options.MaxHistorySize)
            {
                _eventHistory.TryDequeue(out _);
            }
        }
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        var eventList = domainEvents.ToList();

        _logger?.LogDebug("Dispatching {Count} domain events", eventList.Count);

        if (_options.DispatchInParallel)
        {
            var tasks = eventList.Select(e => DispatchAsync(e, cancellationToken));
            await Task.WhenAll(tasks);
        }
        else
        {
            foreach (var domainEvent in eventList)
            {
                await DispatchAsync(domainEvent, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    break;
            }
        }
    }

    public Task<IReadOnlyList<DomainEventRecord>> GetEventHistoryAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var history = _eventHistory
            .OrderByDescending(r => r.DispatchedAt)
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<DomainEventRecord>>(history);
    }

    private async Task DispatchViaMediatorAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (_mediator == null)
            return;

        try
        {
            // MediatR notification dispatch
            await _mediator.Publish(domainEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "MediatR dispatch failed for {EventType}", domainEvent.GetType().Name);

            if (_options.ThrowOnMediatorError)
            {
                throw;
            }
        }
    }

    private async Task DispatchToHandlersAsync(
        IDomainEvent domainEvent,
        Type eventType,
        CancellationToken cancellationToken)
    {
        // Get the generic handler type
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        // Get all registered handlers
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler == null)
                continue;

            try
            {
                // Get the HandleAsync method
                var method = handlerType.GetMethod("HandleAsync");
                if (method != null)
                {
                    var task = (Task?)method.Invoke(handler, new object[] { domainEvent, cancellationToken });
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Handler {Handler} failed for event {EventType}",
                    handler.GetType().Name, eventType.Name);

                if (_options.ThrowOnHandlerError)
                {
                    throw;
                }
            }
        }
    }

    private async Task ForwardToSiemAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (_siemForwarder == null)
            return;

        try
        {
            var securityEvent = MapToSecurityEvent(domainEvent);
            await _siemForwarder.ForwardEventAsync(securityEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to forward domain event {EventType} to SIEM",
                domainEvent.GetType().Name);
        }
    }

    private SecurityEvent MapToSecurityEvent(IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType().Name;

        // Map domain event to security event type
        var securityEventType = eventType switch
        {
            var name when name.Contains("Created") => SecurityEventType.DataModification,
            var name when name.Contains("Updated") => SecurityEventType.DataModification,
            var name when name.Contains("Deleted") => SecurityEventType.DataDeletion,
            var name when name.Contains("Archived") => SecurityEventType.BackupRestore,
            var name when name.Contains("Restored") => SecurityEventType.BackupRestore,
            var name when name.Contains("Auth") => SecurityEventType.Authentication,
            var name when name.Contains("Access") => SecurityEventType.DataAccess,
            var name when name.Contains("Policy") => SecurityEventType.PolicyViolation,
            _ => SecurityEventType.AuditTrail
        };

        var severity = eventType switch
        {
            var name when name.Contains("Error") => SecuritySeverity.Error,
            var name when name.Contains("Failed") => SecuritySeverity.Warning,
            var name when name.Contains("Deleted") => SecuritySeverity.Notice,
            _ => SecuritySeverity.Informational
        };

        // Extract properties from domain event
        var properties = new Dictionary<string, object>();
        foreach (var prop in domainEvent.GetType().GetProperties())
        {
            if (prop.Name == "OccurredAt")
                continue;

            var value = prop.GetValue(domainEvent);
            if (value != null)
            {
                properties[prop.Name] = value;
            }
        }

        return new SecurityEvent(
            EventId: Guid.NewGuid().ToString(),
            Timestamp: domainEvent.OccurredAt,
            EventType: securityEventType,
            Severity: severity,
            Source: "DomainEvents",
            Message: $"Domain event: {eventType}",
            UserId: properties.TryGetValue("UserId", out var userId) ? userId?.ToString() : null,
            SourceIp: null,
            DestinationIp: null,
            ResourcePath: properties.TryGetValue("ResourceId", out var resourceId) ? resourceId?.ToString() : null,
            Action: eventType,
            Outcome: "Success",
            ExtendedProperties: properties);
    }
}

public class DomainEventDispatcherOptions
{
    public bool UseMediator { get; set; } = true;
    public bool ForwardToSiem { get; set; } = false;
    public bool ThrowOnError { get; set; } = false;
    public bool ThrowOnMediatorError { get; set; } = false;
    public bool ThrowOnHandlerError { get; set; } = false;
    public bool DispatchInParallel { get; set; } = false;
    public int MaxHistorySize { get; set; } = 1000;
}

public record DomainEventRecord(
    string EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    DateTimeOffset DispatchedAt,
    bool Success,
    string? ErrorMessage);

public class OutboxDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly ConcurrentQueue<OutboxMessage> _outbox = new();
    private readonly ILogger<OutboxDomainEventDispatcher>? _logger;
    private readonly IDomainEventDispatcher _innerDispatcher;

    public OutboxDomainEventDispatcher(
        IDomainEventDispatcher innerDispatcher,
        ILogger<OutboxDomainEventDispatcher>? logger = null)
    {
        _innerDispatcher = innerDispatcher ?? throw new ArgumentNullException(nameof(innerDispatcher));
        _logger = logger;
    }

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var outboxMessage = new OutboxMessage(
            MessageId: Guid.NewGuid().ToString(),
            EventType: domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().Name,
            Payload: System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            CreatedAt: DateTimeOffset.UtcNow,
            ProcessedAt: null,
            RetryCount: 0);

        _outbox.Enqueue(outboxMessage);
        _logger?.LogDebug("Added domain event {EventType} to outbox", domainEvent.GetType().Name);

        return Task.CompletedTask;
    }

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            DispatchAsync(domainEvent, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public async Task ProcessOutboxAsync(CancellationToken cancellationToken = default)
    {
        while (_outbox.TryDequeue(out var message) && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType == null)
                {
                    _logger?.LogWarning("Could not resolve event type {EventType}", message.EventType);
                    continue;
                }

                var domainEvent = System.Text.Json.JsonSerializer.Deserialize(message.Payload, eventType) as IDomainEvent;
                if (domainEvent == null)
                {
                    _logger?.LogWarning("Could not deserialize domain event {EventType}", message.EventType);
                    continue;
                }

                await _innerDispatcher.DispatchAsync(domainEvent, cancellationToken);

                _logger?.LogDebug("Processed outbox message {MessageId}", message.MessageId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to process outbox message {MessageId}", message.MessageId);

                if (message.RetryCount < 3)
                {
                    _outbox.Enqueue(message with { RetryCount = message.RetryCount + 1 });
                }
            }
        }
    }

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_outbox.Count);
    }
}

public record OutboxMessage(
    string MessageId,
    string EventType,
    string Payload,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    int RetryCount);
