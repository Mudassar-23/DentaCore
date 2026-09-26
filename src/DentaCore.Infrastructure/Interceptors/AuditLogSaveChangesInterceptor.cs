using System.Text.Json;
using DentaCore.Application.Common;
using DentaCore.Domain.Common;
using DentaCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DentaCore.Infrastructure.Interceptors;

public class AuditLogSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditLogSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var context = eventData.Context;
        var now = DateTime.UtcNow;

        // Process IAuditable and ISoftDeletable
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = now;
                }
            }

            if (entry.Entity is ISoftDeletable softDeletable && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                if (entry.Entity is IAuditable softAuditable)
                {
                    softAuditable.UpdatedAt = now;
                }
            }
        }

        // Process AuditLogs for key business entities
        var auditEntries = new List<AuditLog>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.Entity is RefreshToken || entry.Entity is Notification)
                continue;

            if (entry.State != EntityState.Added &&
                entry.State != EntityState.Modified &&
                entry.State != EntityState.Deleted)
                continue;

            var entityName = entry.Entity.GetType().Name;
            var entityId = entry.Entity is BaseEntity baseEntity ? baseEntity.Id : (Guid?)null;
            var action = entry.State.ToString();

            string? oldValue = null;
            string? newValue = null;

            try
            {
                if (entry.State == EntityState.Modified)
                {
                    var oldProps = new Dictionary<string, object?>();
                    var newProps = new Dictionary<string, object?>();

                    foreach (var prop in entry.Properties)
                    {
                        if (prop.IsModified)
                        {
                            oldProps[prop.Metadata.Name] = prop.OriginalValue;
                            newProps[prop.Metadata.Name] = prop.CurrentValue;
                        }
                    }

                    oldValue = JsonSerializer.Serialize(oldProps);
                    newValue = JsonSerializer.Serialize(newProps);
                }
                else if (entry.State == EntityState.Added)
                {
                    var props = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                    newValue = JsonSerializer.Serialize(props);
                }
                else if (entry.State == EntityState.Deleted)
                {
                    var props = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                    oldValue = JsonSerializer.Serialize(props);
                }
            }
            catch
            {
                // In case of non-serializable property, fallback safely
            }

            auditEntries.Add(new AuditLog
            {
                UserId = _currentUserService.UserId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                IpAddress = _currentUserService.IpAddress,
                CreatedAt = now
            });
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
