using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BH_AEP.Application.Common;
using BH_AEP.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BH_AEP.Infrastructure.Services;

public class JwtOptions
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "BH-AEP";
    public string Audience { get; set; } = "BH-AEP-Clients";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(_options.SecretKey));

    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("sid", user.CurrentSessionToken ?? Guid.NewGuid().ToString("N")),
            new("token_use", "access")
        };

        var token = WriteToken(claims, expires);
        return (token, expires);
    }

    public string CreateRefreshToken(User user)
    {
        var expires = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sid", user.CurrentSessionToken ?? Guid.NewGuid().ToString("N")),
            new("token_use", "refresh")
        };

        return WriteToken(claims, expires);
    }

    private string WriteToken(IEnumerable<Claim> claims, DateTime expires)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(descriptor);
        return handler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token, bool isRefreshToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = Key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(token, parameters, out var validatedToken);
            var use = principal.FindFirst("token_use")?.Value;
            var sid = principal.FindFirst("sid")?.Value;
            var uid = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (use != (isRefreshToken ? "refresh" : "access"))
                return null;
            if (string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(uid))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public string? GetSessionId(ClaimsPrincipal principal)
        => principal.FindFirst("sid")?.Value;

    public int? GetUserId(ClaimsPrincipal principal)
        => int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
