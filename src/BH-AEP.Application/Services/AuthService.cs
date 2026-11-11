using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<LoginResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<AuthUserDto> CurrentProfileAsync(CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public const int MinPasswordLength = 8;

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IPermissionService _permissions;
    private readonly INotificationService _notifications;

    public AuthService(
        IAppDbContext db,
        ICurrentUser current,
        IClock clock,
        IPasswordHasher hasher,
        ITokenService tokens,
        IPermissionService permissions,
        INotificationService notifications)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _hasher = hasher;
        _tokens = tokens;
        _permissions = permissions;
        _notifications = notifications;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
                       u.Username == request.Username.Trim() && !u.IsDeleted, ct)
                   ?? throw new UnauthorizedException("اسم المستخدم أو كلمة المرور غير صحيحة.");

        if (user.IsLocked)
        {
            if (user.LockedUntil.HasValue && user.LockedUntil > _clock.Now)
                throw new UnauthorizedException("الحساب مقفل مؤقتًا بسبب محاولات خاطئة. حاول لاحقًا.");
            user.IsLocked = false;
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            await _notifications.CreateAsync(NotificationType.FailedLogin, "User", user.Id,
                $"محاولة دخول فاشلة للحساب «{user.Username}» (المحاولة {user.FailedLoginAttempts}).", ct);

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.IsLocked = true;
                user.LockedUntil = _clock.Now.Add(LockoutDuration);
                await _notifications.CreateAsync(NotificationType.UserLockedOut, "User", user.Id,
                    $"تم قفل الحساب «{user.Username}» لمدة 15 دقيقة بعد تجاوز عدد المحاولات الفاشلة.", ct);
            }

            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedException("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        user.FailedLoginAttempts = 0;
        user.IsLocked = false;
        user.LockedUntil = null;
        user.CurrentSessionToken = Guid.NewGuid().ToString("N");
        user.LastLoginAt = _clock.Now;

        await _db.SaveChangesAsync(ct);
        var perms = await _permissions.GetPermissionsForUserAsync(user.Id, user.Role, ct);
        return BuildLoginResponse(user, perms);
    }

    public async Task<LoginResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var principal = _tokens.ValidateToken(request.RefreshToken, true)
                        ?? throw new UnauthorizedException("رمز التحديث غير صالح.");

        var userId = _tokens.GetUserId(principal);
        var sid = _tokens.GetSessionId(principal);
        if (userId is null || sid is null)
            throw new UnauthorizedException("رمز التحديث غير صالح.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                   ?? throw new UnauthorizedException("المستخدم غير موجود.");

        if (user.IsLocked)
            throw new UnauthorizedException("الحساب مقفل.");

        if (user.CurrentSessionToken != sid)
            throw new UnauthorizedException("انتهت الجلسة — تم تسجيل الدخول من مكان آخر أو تسجيل الخروج.");

        user.LastLoginAt = _clock.Now;
        await _db.SaveChangesAsync(ct);
        var perms = await _permissions.GetPermissionsForUserAsync(user.Id, user.Role, ct);
        return BuildLoginResponse(user, perms);
    }

    public async Task<LoginResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            throw new UnauthorizedException();

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            throw new ValidationException2($"كلمة المرور يجب ألا تقل عن {MinPasswordLength} أحرف.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _current.UserId.Value && !u.IsDeleted, ct)
                   ?? throw new UnauthorizedException();

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException2("كلمة المرور الحالية غير صحيحة.");

        if (_hasher.Verify(request.NewPassword, user.PasswordHash))
            throw new ValidationException2("كلمة المرور الجديدة يجب أن تختلف عن الحالية.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.ForcePasswordChange = false;
        user.CurrentSessionToken = Guid.NewGuid().ToString("N");
        user.FailedLoginAttempts = 0;
        user.IsLocked = false;
        user.LockedUntil = null;

        await _db.SaveChangesAsync(ct);
        var perms = await _permissions.GetPermissionsForUserAsync(user.Id, user.Role, ct);
        return BuildLoginResponse(user, perms);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            return;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _current.UserId.Value, ct);
        if (user is not null)
        {
            user.CurrentSessionToken = null;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<AuthUserDto> CurrentProfileAsync(CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            throw new UnauthorizedException();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _current.UserId.Value && !u.IsDeleted, ct)
                   ?? throw new UnauthorizedException();

        var perms = await _permissions.GetEffectivePermissionsAsync(ct);
        return new AuthUserDto(user.Id, user.Username, user.Role.ToString(), user.ForcePasswordChange, perms);
    }

    private LoginResponse BuildLoginResponse(User user, IReadOnlyList<string> permissions)
    {
        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken(user);

        return new LoginResponse(
            access.Token,
            refresh,
            access.ExpiresAt,
            new AuthUserDto(
                user.Id,
                user.Username,
                user.Role.ToString(),
                user.ForcePasswordChange,
                permissions));
    }
}
