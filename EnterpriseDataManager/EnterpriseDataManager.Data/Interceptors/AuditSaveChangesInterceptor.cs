namespace EnterpriseDataManager.Data.Interceptors;

using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly List<AuditEntry> _pendingAuditEntries = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            CollectAuditEntries(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            CollectAuditEntries(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && _pendingAuditEntries.Count > 0)
        {
            await SaveAuditEntriesAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null && _pendingAuditEntries.Count > 0)
        {
            SaveAuditEntriesAsync(eventData.Context, default).GetAwaiter().GetResult();
        }

        return base.SavedChanges(eventData, result);
    }

    private void CollectAuditEntries(DbContext context)
    {
        _pendingAuditEntries.Clear();

        var entries = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditRecord)
            .ToList();

        foreach (var entry in entries)
        {
            var auditEntry = new AuditEntry
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id.ToString(),
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Modified => "Updated",
                    EntityState.Deleted => "Deleted",
                    _ => "Unknown"
                },
                Timestamp = DateTimeOffset.UtcNow,
                Actor = GetActor(entry),
                Entry = entry
            };

            if (entry.State == EntityState.Modified)
            {
                auditEntry.OldValues = GetOldValues(entry);
                auditEntry.NewValues = GetNewValues(entry);
            }
            else if (entry.State == EntityState.Added)
            {
                auditEntry.NewValues = GetNewValues(entry);
            }
            else if (entry.State == EntityState.Deleted)
            {
                auditEntry.OldValues = GetOldValues(entry);
            }

            _pendingAuditEntries.Add(auditEntry);
        }
    }

    private static string? GetActor(EntityEntry<BaseEntity> entry)
    {
        if (entry.Entity is IAuditable auditable)
        {
            return entry.State == EntityState.Added
                ? auditable.CreatedBy
                : auditable.UpdatedBy ?? auditable.CreatedBy;
        }
        return null;
    }

    private static Dictionary<string, object?> GetOldValues(EntityEntry entry)
    {
        var values = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (ShouldAuditProperty(property))
            {
                values[property.Metadata.Name] = property.OriginalValue;
            }
        }

        return values;
    }

    private static Dictionary<string, object?> GetNewValues(EntityEntry entry)
    {
        var values = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (ShouldAuditProperty(property))
            {
                values[property.Metadata.Name] = property.CurrentValue;
            }
        }

        return values;
    }

    private static bool ShouldAuditProperty(PropertyEntry property)
    {
        var excludedProperties = new HashSet<string>
        {
            "Id", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy",
            "DeletedAt", "DeletedBy", "IsDeleted", "RowVersion"
        };

        return !excludedProperties.Contains(property.Metadata.Name)
               && !property.Metadata.IsShadowProperty();
    }

    private async Task SaveAuditEntriesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var auditRecords = _pendingAuditEntries.Select(CreateAuditRecord).ToList();
        _pendingAuditEntries.Clear();

        foreach (var record in auditRecords)
        {
            context.Set<AuditRecord>().Add(record);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static AuditRecord CreateAuditRecord(AuditEntry entry)
    {
        string? oldValuesJson = entry.OldValues?.Count > 0
            ? JsonSerializer.Serialize(entry.OldValues, JsonOptions)
            : null;

        string? newValuesJson = entry.NewValues?.Count > 0
            ? JsonSerializer.Serialize(entry.NewValues, JsonOptions)
            : null;

        var details = BuildDetailsString(entry.Action, oldValuesJson, newValuesJson);

        return AuditRecord.Create(entry.Actor ?? "System", entry.Action, success: true)
            .WithResource(entry.EntityType, entry.EntityId)
            .WithDetails(details);
    }

    private static string BuildDetailsString(string action, string? oldValues, string? newValues)
    {
        var parts = new List<string> { $"Entity {action.ToLowerInvariant()}" };

        if (!string.IsNullOrEmpty(oldValues))
        {
            parts.Add($"Old: {oldValues}");
        }

        if (!string.IsNullOrEmpty(newValues))
        {
            parts.Add($"New: {newValues}");
        }

        return string.Join(" | ", parts);
    }

    private sealed class AuditEntry
    {
        public required string EntityType { get; init; }
        public required string EntityId { get; init; }
        public required string Action { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
        public string? Actor { get; init; }
        public EntityEntry? Entry { get; init; }
        public Dictionary<string, object?>? OldValues { get; set; }
        public Dictionary<string, object?>? NewValues { get; set; }
    }
}
