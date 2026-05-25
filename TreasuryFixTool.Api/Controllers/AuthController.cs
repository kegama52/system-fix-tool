using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TreasuryFixTool.Api.Data;
using TreasuryFixTool.Api.Models;
using TreasuryFixTool.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TreasuryFixTool.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User>    _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtService           _jwtService;
    private readonly AppDbContext          _db;

    public AuthController(
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IJwtService jwtService,
        AppDbContext db)
    {
        _userManager  = userManager;
        _roleManager  = roleManager;
        _jwtService   = jwtService;
        _db           = db;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  POST /api/auth/login
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByNameAsync(req.UserName);
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<AuthResponse>.Fail("Invalid credentials."));

        if (await _userManager.IsLockedOutAsync(user))
            return StatusCode(423, ApiResponse<AuthResponse>
                .Fail("Account locked. Contact ICTSU to regain access."));

        if (!await _userManager.CheckPasswordAsync(user, req.Password))
        {
            await AuditHelper.WriteAuditAsync(_db, user.Id, "LoginFailed", nameof(User),
                user.Id, null, null, AuditHelper.GetClientIp(HttpContext), AuditHelper.GetUserAgent(), false,
                "Wrong password or locked-out account");
            return Unauthorized(ApiResponse<AuthResponse>.Fail("Invalid credentials."));
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var accessToken  = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            Token        = refreshToken,
            UserId       = user.Id,
            ExpiresAt    = DateTime.UtcNow.AddDays(7),
            CreatedByIp  = AuditHelper.GetClientIp(HttpContext)
        });
        await _db.SaveChangesAsync();

        await AuditHelper.WriteAuditAsync(_db, user.Id, "Login", nameof(User), user.Id,
            null, null, AuditHelper.GetClientIp(HttpContext), AuditHelper.GetUserAgent(), true);

        var resp = new AuthResponse
        {
            AccessToken     = accessToken,
            RefreshToken    = refreshToken,
            ExpiresInSeconds = 60 * 60,
            UserName        = user.UserName ?? string.Empty,
            FullName        = user.FullName,
            Email           = user.Email ?? string.Empty,
            Roles           = roles
        };

        return Ok(ApiResponse<AuthResponse>.Ok(resp));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  POST /api/auth/register
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("register")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<UserDto>.Fail("Invalid request."));

        if (await _userManager.FindByNameAsync(req.UserName) is not null)
            return Conflict(ApiResponse<UserDto>.Fail($"Username '{req.UserName}' already exists."));

        if (await _userManager.FindByEmailAsync(req.Email) is not null)
            return Conflict(ApiResponse<UserDto>.Fail($"Email '{req.Email}' already registered."));

        var role = await ValidateRoleAsync(req.Role);

        var user = new User
        {
            UserName   = req.UserName,
            Email      = req.Email,
            FullName   = req.FullName,
            Department = req.Department ?? string.Empty,
            IsActive   = true
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<UserDto>.Fail(
                string.Join("; ", result.Errors.Select(e => e.Description))));

        await _userManager.AddToRoleAsync(user, role);
        await AuditHelper.WriteAuditAsync(_db, HttpContext.GetUserId(), "Register", nameof(User),
            user.Id, null, null, AuditHelper.GetClientIp(HttpContext), AuditHelper.GetUserAgent(), true);

        return CreatedAtAction(nameof(GetUserById), new { id = user.Id },
            ApiResponse<UserDto>.Ok(UserMapper.ToDto(user, await _userManager.GetRolesAsync(user))));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  POST /api/auth/refresh
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshTokenRequest req)
    {
        if (!await _db.Set<RefreshToken>().AnyAsync(r => r.Token == req.RefreshToken))
            return Unauthorized(ApiResponse<AuthResponse>.Fail("Refresh token not recognised or revoked."));

        var rawToken = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityTokenHandler().ReadJwtToken(req.RefreshToken));
        var principal = _jwtService.ValidateToken(rawToken);
        if (principal is null)
            return Unauthorized(ApiResponse<AuthResponse>.Fail("Refresh token is invalid or expired."));

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await _userManager.FindByIdAsync(userId!);
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<AuthResponse>.Fail("Associated user is inactive."));

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var newAccess  = _jwtService.GenerateAccessToken(user, roles);
        var newRefresh = _jwtService.GenerateRefreshToken();

        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            Token        = newRefresh,
            UserId       = user.Id,
            ExpiresAt    = DateTime.UtcNow.AddDays(7),
            CreatedByIp  = AuditHelper.GetClientIp(HttpContext)
        });
        await _db.SaveChangesAsync();

        await AuditHelper.WriteAuditAsync(_db, user.Id, "RefreshToken", nameof(User), user.Id,
            null, null, AuditHelper.GetClientIp(HttpContext), AuditHelper.GetUserAgent(), true);

        return Ok(ApiResponse<AuthResponse>.Ok(new AuthResponse
        {
            AccessToken      = newAccess,
            RefreshToken     = newRefresh,
            ExpiresInSeconds = 60 * 60,
            UserName         = user.UserName ?? string.Empty,
            FullName         = user.FullName,
            Email            = user.Email ?? string.Empty,
            Roles            = roles
        }));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  POST /api/auth/logout
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout([FromBody] LogoutRequest req)
    {
        var token = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken);

        if (token is not null) { _db.Set<RefreshToken>().Remove(token); await _db.SaveChangesAsync(); }

        await AuditHelper.WriteAuditAsync(_db, HttpContext.GetUserId(), "Logout", nameof(User),
            HttpContext.GetUserId(), null, null, AuditHelper.GetClientIp(HttpContext), AuditHelper.GetUserAgent(), true);

        return Ok(ApiResponse<bool>.Ok(true, "Logged out successfully."));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  POST /api/auth/change-password
    // ══════════════════════════════════════════════════════════════════════════
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<bool>.Fail(
                string.Join("; ", result.Errors.Select(e => e.Description))));

        await AuditHelper.WriteAuditAsync(_db, user.Id, "PasswordChanged", nameof(User), user.Id,
            null, null, AuditHelper.GetClientIp(HttpContext), AuditHelper.GetUserAgent(), true);
        return Ok(ApiResponse<bool>.Ok(true, "Password changed."));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/auth/me
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(ApiResponse<UserDto>.Ok(UserMapper.ToDto(user, roles)));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GET /api/auth/users/{id}
    // ══════════════════════════════════════════════════════════════════════════
    [HttpGet("users/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound(ApiResponse<UserDto>.Fail("User not found."));
        return Ok(ApiResponse<UserDto>.Ok(UserMapper.ToDto(user, await _userManager.GetRolesAsync(user))));
    }

    // ─────────────────────────────────────────────────────────────────────────
    private async Task<string> ValidateRoleAsync(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "Viewer";
        var lowered = role.Trim();
        if (lowered.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || lowered.Equals("Technician", StringComparison.OrdinalIgnoreCase)
            || lowered.Equals("Viewer",    StringComparison.OrdinalIgnoreCase))
            return char.ToUpper(lowered[0]) + lowered[1..].ToLower();
        throw new ArgumentException($"Invalid role '{role}'. Allowed: Admin, Technician, Viewer.");
    }
}
