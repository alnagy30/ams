using BH_AEP.Domain.Enums;

namespace BH_AEP.Application.DTOs;

public record LoginRequest(string Username, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record AuthUserDto(
    int Id,
    string Username,
    string Role,
    bool ForcePasswordChange,
    IReadOnlyList<string> Permissions);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    AuthUserDto User);

public record UserDto(
    int Id,
    string Username,
    string Role,
    bool IsLocked,
    bool ForcePasswordChange,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record CreateUserRequest(string Username, string Password, string Role);

public record UpdateUserRequest(string? Username, string Role, bool? IsLocked);

public record AdminResetPasswordRequest(int UserId, string NewPassword);

public record TemporaryPermissionDto(
    int Id,
    int UserId,
    string UserName,
    string Permission,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsRevoked);

public record GrantTemporaryPermissionRequest(int UserId, string Permission, DateTime StartsAt, DateTime EndsAt);
