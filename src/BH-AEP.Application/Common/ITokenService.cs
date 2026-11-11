using System.Security.Claims;
using BH_AEP.Domain.Entities;

namespace BH_AEP.Application.Common;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
    string CreateRefreshToken(User user);
    ClaimsPrincipal? ValidateToken(string token, bool isRefreshToken);
    string? GetSessionId(ClaimsPrincipal principal);
    int? GetUserId(ClaimsPrincipal principal);
}
