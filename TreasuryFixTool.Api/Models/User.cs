using Microsoft.AspNetCore.Identity;

namespace TreasuryFixTool.Api.Models;

/// <summary>
/// Application user — extends IdentityUser with staff-fields relevant to the Treasury support tool.
/// </summary>
public class User : IdentityUser
{
    /// <summary>Full name as it appears in the organisation directory.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Department this staff member belongs to.</summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>True when the account is active and may sign in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Timestamp of the last successful login (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Timestamp of the last lockout (UTC).</summary>
    public DateTime? LockedOutAt { get; set; }

    /// <summary>Free-text reason for the most recent deactivation / lockout.</summary>
    public string? LockoutReason { get; set; }

    /// <summary>Audit log entries attributable to this user.</summary>
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    /// <summary>Refresh tokens issued to this user.</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
