using Microsoft.AspNetCore.Http;
using System.Text.Json;
using TreasuryFixTool.Api.Data;
using TreasuryFixTool.Api.Models;

namespace TreasuryFixTool.Api.Services;

public static class AuditHelper
{
    public static string GetUserAgent()
        => Environment.GetEnvironmentVariable("HTTP_USER_AGENT") ?? "unknown";

    public static string GetClientIp(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static async Task WriteAuditAsync(
        AppDbContext db,
        string? userId,
        string action,
        string entityType,
        string? entityId,
        string? oldVal,
        string? newVal,
        string ip,
        string ua,
        bool success,
        string? err = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId       = userId,
            Action       = action,
            EntityType   = entityType,
            EntityId     = entityId,
            OldValue     = oldVal,
            NewValue     = newVal,
            IpAddress    = ip,
            UserAgent    = ua,
            Success      = success,
            ErrorMessage = err,
            CreatedAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}