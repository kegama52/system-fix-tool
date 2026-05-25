namespace TreasuryFixTool.Api.Models;

/// <summary>Server-side refresh-token store — supports revocation.</summary>
public class RefreshToken
{
    public int         Id        { get; set; }
    public string      Token     { get; set; } = string.Empty;
    public string      UserId    { get; set; } = string.Empty;
    public User        User      { get; set; } = null!;
    public DateTime    ExpiresAt { get; set; }
    public bool        Revoked   { get; set; }
    public DateTime?   RevokedAt { get; set; }
    public string     CreatedByIp { get; set; } = string.Empty;
    public DateTime   CreatedAt   { get; set; } = DateTime.UtcNow;
}
