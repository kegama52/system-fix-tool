using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using TreasuryFixTool.Api.Data;
using TreasuryFixTool.Api.Models;
using TreasuryFixTool.Api.Services;
using System.Text.Json;

namespace TreasuryFixTool.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<User>    _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext         _db;

    public UsersController(
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db)
    {
        _userManager  = userManager;
        _roleManager  = roleManager;
        _db           = db;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/users
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet]
    [Authorize(Policy = "TechOrAbove")]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var callerRoles = (await _userManager.GetRolesAsync(await _userManager.GetUserAsync(User) ?? new User())).ToList();

        IQueryable<User> query = _db.Users.AsNoTracking();
        if (!includeInactive) query = query.Where(u => u.IsActive);

        var users = await query
            .OrderByDescending(u => u.LastLoginAt)
            .ToListAsync();

        var dtos = new List<UserDto>();
        foreach (var u in users)
            dtos.Add(UserMapper.ToDto(u, await _userManager.GetRolesAsync(u)));

        if (!callerRoles.Contains("Admin"))
        {
            foreach (var d in dtos) { d.Email = "(hidden)"; }
        }

        return Ok(ApiResponse<List<UserDto>>.Ok(dtos));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/users/{id}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("{id}")]
    [Authorize(Policy = "TechOrAbove")]
public async Task<ActionResult<ApiResponse<UserDto>>> GetById(string id)
     {
         var user = await _userManager.FindByIdAsync(id);
         if (user is null) return NotFound(ApiResponse<UserDto>.Fail("User not found."));

         var caller      = await _userManager.GetUserAsync(User);
         if (caller is null) return Unauthorized(ApiResponse<UserDto>.Fail("User not authenticated."));
         var callerRoles = (await _userManager.GetRolesAsync(caller)).ToList();
         if (!callerRoles.Contains("Admin") && caller.Id != id)
             return StatusCode(403, ApiResponse<UserDto>.Fail("You may only view your own account."));

         return Ok(ApiResponse<UserDto>.Ok(
             UserMapper.ToDto(user, await _userManager.GetRolesAsync(user))));
     }

    // ══════════════════════════════════════════════════════════════════════════
    //  POST /api/users
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] UpsertUserRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<UserDto>.Fail("Validation failed."));

        if (await _userManager.FindByNameAsync(req.UserName) is not null)
            return Conflict(ApiResponse<UserDto>.Fail("Username already taken."));

        if (await _userManager.FindByEmailAsync(req.Email) is not null)
            return Conflict(ApiResponse<UserDto>.Fail("Email already registered."));

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password!.Length < 8)
            return BadRequest(ApiResponse<UserDto>.Fail("Password must be at least 8 characters."));

        var user = new User
        {
            UserName   = req.UserName,
            Email      = req.Email,
            FullName   = req.FullName,
            Department = req.Department ?? string.Empty,
            IsActive   = req.IsActive
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<UserDto>.Fail(
                string.Join("; ", result.Errors.Select(e => e.Description))));

        var roles = await ValidateAndAssignRolesAsync(req.Roles, user.Id);

        using var auditScope = HttpContext.RequestServices.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        auditDb.AuditLogs.Add(new AuditLog
        {
            UserId     = HttpContext.GetUserId(),
            Action     = "Create",
            EntityType = nameof(User),
            EntityId   = user.Id,
            NewValue   = JsonSerializer.Serialize(UserMapper.ToDto(user, roles)),
            IpAddress  = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent  = AuditHelper.GetUserAgent(), Success = true,
            CreatedAt  = DateTime.UtcNow
        });
        await auditDb.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            ApiResponse<UserDto>.Ok(UserMapper.ToDto(user, roles), "Account created successfully."));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PUT /api/users/{id}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(string id, [FromBody] UpsertUserRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<UserDto>.Fail("Validation failed."));

        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(ApiResponse<UserDto>.Fail("User not found."));

        var oldSnapshot = JsonSerializer.Serialize(UserMapper.ToDto(user, await _userManager.GetRolesAsync(user)));

        user.FullName   = req.FullName;
        user.Email      = req.Email;
        user.UserName   = req.UserName;
        user.Department = req.Department ?? string.Empty;
        user.IsActive   = req.IsActive;

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var pwResult = await _userManager.ResetPasswordAsync(user, token, req.Password!);
            if (!pwResult.Succeeded)
                return BadRequest(ApiResponse<UserDto>.Fail(
                    string.Join("; ", pwResult.Errors.Select(e => e.Description))));
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return BadRequest(ApiResponse<UserDto>.Fail(
                string.Join("; ", updateResult.Errors.Select(e => e.Description))));

        var newRoles = await ValidateAndAssignRolesAsync(req.Roles, user.Id);

        using var auditScope = HttpContext.RequestServices.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        auditDb.AuditLogs.Add(new AuditLog
        {
            UserId     = HttpContext.GetUserId(),
            Action     = "Update",
            EntityType = nameof(User),
            EntityId   = user.Id,
            OldValue   = oldSnapshot,
            NewValue   = JsonSerializer.Serialize(UserMapper.ToDto(user, newRoles)),
            IpAddress  = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent  = AuditHelper.GetUserAgent(), Success = true,
            CreatedAt  = DateTime.UtcNow
        });
        await auditDb.SaveChangesAsync();

        return Ok(ApiResponse<UserDto>.Ok(UserMapper.ToDto(user, newRoles), "Account updated."));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PATCH /api/users/{id}/status
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPatch("{id}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateStatus(
        string id, [FromBody] UpdateUserStatusRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(ApiResponse<UserDto>.Fail("User not found."));

        var oldSnapshot = JsonSerializer.Serialize(UserMapper.ToDto(user, await _userManager.GetRolesAsync(user)));

        if (req.IsActive.HasValue && req.IsActive.Value)
        {
            user.LockoutEnd      = null;
            user.LockoutReason   = null;
            user.LockedOutAt     = null;
            await _userManager.SetLockoutEnabledAsync(user, true);
        }

        if (req.IsActive.HasValue && !req.IsActive.Value)
        {
            user.LockoutEnd    = DateTimeOffset.MaxValue;
            user.LockoutReason = req.LockoutReason ?? "Deactivated by administrator.";
            user.LockedOutAt   = DateTime.UtcNow;
        }

        if (req.LockoutEnd.HasValue)
            user.LockoutEnd = req.LockoutEnd.Value;

        if (req.LockoutReason is not null)
            user.LockoutReason = req.LockoutReason;

        if (req.Roles is not null && req.Roles.Any())
            await ValidateAndAssignRolesAsync(req.Roles, user.Id);

        await _userManager.UpdateAsync(user);

        await AuditHelper.WriteAuditAsync(_db,
            HttpContext.GetUserId()!, "UpdateStatus", nameof(User),
            user.Id,
            oldVal:     oldSnapshot,
            newVal:     JsonSerializer.Serialize(UserMapper.ToDto(user, await _userManager.GetRolesAsync(user))),
            ip:         HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            ua:         AuditHelper.GetUserAgent(),
            success:    true);

        return Ok(ApiResponse<UserDto>.Ok(
            UserMapper.ToDto(user, await _userManager.GetRolesAsync(user))));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DELETE /api/users/{id}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<bool>>> SoftDelete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(ApiResponse<bool>.Fail("User not found."));

        user.IsActive    = false;
        user.LockoutEnd  = DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);

        await AuditHelper.WriteAuditAsync(_db,
            userId:     HttpContext.GetUserId()!,
            action:     "SoftDelete",
            entityType: nameof(User),
            entityId:   user.Id,
            oldVal:     $"IsActive=true",
            newVal:     "IsActive=false",
            ip:         HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            ua:         AuditHelper.GetUserAgent(),
            success:    true);

        return Ok(ApiResponse<bool>.Ok(true, "User deactivated."));
    }

    // ─────────────────────────────────────────────────────────────────────────
    private async Task<string[]> ValidateAndAssignRolesAsync(List<string> requested, string userId)
    {
        var valid = new[] { "Admin", "Technician", "Viewer" };
        var sanitized = requested
            .Where(r => valid.Contains(r, StringComparer.OrdinalIgnoreCase))
            .Select(r => r switch
            {
                "Admin"      => "Admin",
                "Technician" => "Technician",
                "Viewer"     => "Viewer",
                _            => r
            })
            .Distinct()
            .ToList();

if (!sanitized.Any()) sanitized.Add("Viewer");

         var user = await _userManager.FindByIdAsync(userId);
         if (user is null) return Array.Empty<string>();

         var existing = (await _userManager.GetRolesAsync(user)).ToList();

         var toRemove = existing.Except(sanitized);
         var toAdd    = sanitized.Except(existing);

         foreach (var r in toRemove) await _userManager.RemoveFromRoleAsync(user, r);
         foreach (var r in toAdd)    await _userManager.AddToRoleAsync(user, r);

         return sanitized.ToArray();
     }
 }
