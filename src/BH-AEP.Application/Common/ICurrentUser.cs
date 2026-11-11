using BH_AEP.Domain.Enums;

namespace BH_AEP.Application.Common;

public interface ICurrentUser
{
    int? UserId { get; }
    UserRole? Role { get; }
    string? IpAddress { get; }
    string? SessionId { get; }
    bool IsAuthenticated { get; }
}
