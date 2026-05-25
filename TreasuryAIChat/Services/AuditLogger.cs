using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TreasuryAIChat.Data;
using TreasuryAIChat.Models;

namespace TreasuryAIChat.Services;

/// <summary>One structured audit event.</summary>
public record AuditEntry(string EventType, string ConversationId, string Summary)
{
    public Guid        Id          { get; } = Guid.NewGuid();
    public DateTimeOffset At       { get; init; } = DateTimeOffset.UtcNow;
    public string      EventType   { get; init; } = EventType;
    public string      ConversationId { get; init; } = ConversationId;
    public string      Summary     { get; init; } = Summary;
}

/// <summary>Persistent audit writer — one tag/partition per conversation.</summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
}

public class PostgreAuditLogger : IAuditLogger
{
    private readonly IDbContextFactory<TasksDbContext> _dbFactory;
    private readonly ILogger<PostgreAuditLogger>       _log;

    public PostgreAuditLogger(IDbContextFactory<TasksDbContext> dbFactory, ILogger<PostgreAuditLogger> log)
    {
        _dbFactory = dbFactory;
        _log       = log;
    }

        public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
        {
            try
            {
                await using var db = _dbFactory.CreateDbContext();
            db.AuditLogs.Add(new AuditLogEntity
            {
                Id            = entry.Id,
                At            = entry.At.UtcDateTime,
                EventType     = entry.EventType,
                ConversationId = entry.ConversationId,
                Summary       = entry.Summary
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to persist audit entry {EventType}", entry.EventType);
        }
    }
}
