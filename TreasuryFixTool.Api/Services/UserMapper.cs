using TreasuryFixTool.Api.Models;

namespace TreasuryFixTool.Api.Services;

public static class UserMapper
{
    public static UserDto ToDto(User u, IList<string> roles)
        => new()
        {
            Id         = u.Id,
            UserName   = u.UserName   ?? string.Empty,
            Email      = u.Email      ?? string.Empty,
            FullName   = u.FullName,
            Department = u.Department,
            Roles      = roles.ToList(),
            IsActive   = u.IsActive,
            LockoutEnd = u.LockoutEnd,
            LastLoginAt = u.LastLoginAt
        };
}
