using System.Security.Claims;
using BH_AEP.Application.Common;
using BH_AEP.Domain.Enums;

namespace BH_AEP.Web.Services;

public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http)
    {
        _http = http;
    }

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public int? UserId
        => int.TryParse(Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public UserRole? Role
        => Enum.TryParse<UserRole>(Principal?.FindFirst(ClaimTypes.Role)?.Value, out var role) ? role : null;

    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? SessionId => Principal?.FindFirst("sid")?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
