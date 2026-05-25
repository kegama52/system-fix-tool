using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TreasuryFixTool.Api.Data;
using TreasuryFixTool.Api.Models;

namespace TreasuryFixTool.Api.Services;

/// <summary>Issues, validates and refreshes JWT access-tokens.</summary>
public interface IJwtService
{
    string GenerateAccessToken(User user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}

public class JwtService : IJwtService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtService(IConfiguration config)
    {
        _key          = config["Jwt:Key"]          ?? throw new InvalidOperationException("Jwt:Key missing");
        _issuer       = config["Jwt:Issuer"]        ?? "TreasuryFixTool.Api";
        _audience     = config["Jwt:Audience"]      ?? "TreasuryFixTool.Client";
        _expiryMinutes= int.Parse(config["Jwt:AccessTokenMinutes"] ?? "60");
    }

    public string GenerateAccessToken(User user, IList<string> roles)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds      = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name,              user.UserName ?? string.Empty),
            new Claim("full_name",                  user.FullName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer:   _issuer,
            audience: _audience,
            claims:   claims,
            notBefore: DateTime.UtcNow,
            expires:  DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        Random.Shared.NextBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler  = new JwtSecurityTokenHandler();
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));

            var result = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime         = true,
                ValidIssuer              = _issuer,
                ValidAudience            = _audience,
                IssuerSigningKey         = signingKey,
                ClockSkew                = TimeSpan.FromSeconds(30)
            }, out _);

            return result;
        }
        catch
        {
            return null;
        }
    }
}
