namespace TreasuryFixTool.Api.Models;

/// <summary>
/// Immutable audit trail for login events, user CRUD, and role changes.
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    /// <summary>FK – null when the actor is the system / bootstrap seed.</summary>
    public string? UserId { get; set; }
    public User? User { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }      // JSON-serialised state before change
    public string? NewValue { get; set; }      // JSON-serialised state after change
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
