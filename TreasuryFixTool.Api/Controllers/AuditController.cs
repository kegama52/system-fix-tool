using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TreasuryFixTool.Api.Data;
using TreasuryFixTool.Api.Models;

namespace TreasuryFixTool.Api.Controllers;

/// <summary>
/// Read-only audit trail endpoint. Returns all login events, user changes, and
/// role assignments so that ICTSU can review who changed what and when.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db) => _db = db;

    /// <summary>GET /api/audit?userId=&action=&from=&to=&pageSize=&page</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditDto>>>> GetAll(
        [FromQuery] string? userId,
        [FromQuery] string? action,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int pageSize  = 50,
        [FromQuery] int page      = 1)
    {
        if (pageSize < 1 || pageSize > 200) pageSize = 50;
        if (page < 1) page = 1;

        var query = _db.AuditLogs.AsNoTracking()
                      .Include(a => a.User)
                      .AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => EF.Functions.ILike(a.Action, $"%{action}%"));

        DateTime? fromDt = null, toDt = null;
        if (DateTime.TryParse(from, out var f)) fromDt = f.ToUniversalTime();
        if (DateTime.TryParse(to,   out var t)) toDt   = t.ToUniversalTime();
        if (fromDt.HasValue) query = query.Where(a => a.CreatedAt >= fromDt.Value);
        if (toDt.HasValue)   query = query.Where(a => a.CreatedAt <= toDt.Value);

        var total = await query.LongCountAsync();
        var logs  = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditDto
            {
                Id         = a.Id,
                UserName   = a.User != null ? a.User.UserName : "(system)",
                Action     = a.Action,
                EntityType = a.EntityType,
                EntityId   = a.EntityId,
                IpAddress  = a.IpAddress,
                Success    = a.Success,
                CreatedAt  = a.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<PagedResult<AuditDto>>.Ok(
            new(total, logs)));
    }

    /// <summary>GET /api/audit/user/{userId} — filter by specific user</summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<ApiResponse<List<AuditDto>>>> GetForUser(string userId)
    {
        var logs = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(500)
            .Select(a => new AuditDto
            {
                Id         = a.Id,
                UserName   = a.User != null ? a.User.UserName : "(system)",
                Action     = a.Action,
                EntityType = a.EntityType,
                EntityId   = a.EntityId,
                IpAddress  = a.IpAddress,
                Success    = a.Success,
                CreatedAt  = a.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<AuditDto>>.Ok(logs));
    }
}

// ── helpers ──────────────────────────────────────────────────────────────────
public record PagedResult<T>(long Total, List<T> Items);
