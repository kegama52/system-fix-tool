using System.ComponentModel.DataAnnotations;

namespace TreasuryFixTool.Api.Models;

// ── Auth DTOs ─────────────────────────────────────────────────────────────────

public class LoginRequest
{
    [Required] public string UserName { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required] public string UserName { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Role { get; set; }   // "Admin" | "Technician" | "Viewer"
}

public class RefreshTokenRequest
{
    [Required] public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    [Required] public string RefreshToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required] public string NewPassword { get; set; } = string.Empty;
}

// ── User Management DTOs ───────────────────────────────────────────────────────

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UpsertUserRequest
{
    [Required] public string UserName { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string FullName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? Department { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateUserStatusRequest
{
    public bool? IsActive { get; set; }
    public List<string>? Roles { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public string? LockoutReason { get; set; }
}

// ── Audit DTOs ────────────────────────────────────────────────────────────────

public class AuditDto
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public bool Success { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Common ────────────────────────────────────────────────────────────────────

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public static ApiResponse<T> Ok(T data, string? msg = null)
        => new() { Success = true, Message = msg ?? "Success", Data = data };
    public static ApiResponse<T> Fail(string msg)
        => new() { Success = false, Message = msg };
}
