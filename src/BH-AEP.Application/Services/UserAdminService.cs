using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(AdminResetPasswordRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TemporaryPermissionDto>> ListTempPermissionsAsync(CancellationToken ct = default);
    Task GrantTemporaryPermissionAsync(GrantTemporaryPermissionRequest request, CancellationToken ct = default);
    Task RevokeTemporaryPermissionAsync(int permissionId, CancellationToken ct = default);
}

public class UserAdminService : IUserAdminService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;

    public UserAdminService(
        IAppDbContext db,
        ICurrentUser current,
        IClock clock,
        IPasswordHasher hasher,
        IAuditService audit,
        IPermissionService permissions)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _hasher = hasher;
        _audit = audit;
        _permissions = permissions;
    }

    private void RequireAdmin()
    {
        if (_current.Role != UserRole.Admin)
            throw new ForbiddenException("إدارة المستخدمين متاحة للادمن فقط.");
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        RequireAdmin();
        return await _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Id)
            .Select(u => new UserDto(
                u.Id, u.Username, u.Role.ToString(), u.IsLocked,
                u.ForcePasswordChange, u.CreatedAt, u.LastLoginAt))
            .ToListAsync(ct);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        RequireAdmin();

        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ValidationException2("اسم المستخدم مطلوب.");

        if (request.Password.Length < AuthService.MinPasswordLength)
            throw new ValidationException2($"كلمة المرور يجب ألا تقل عن {AuthService.MinPasswordLength} أحرف.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.Admin)
            throw new ValidationException2("الدور غير صحيح — يُسمح بإنشاء AssetManager أو Viewer فقط.");

        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username.Trim() && !u.IsDeleted, ct);
        if (exists)
            throw new ValidationException2("اسم المستخدم مستخدم بالفعل.");

        var user = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = role,
            ForcePasswordChange = true,
            CreatedAt = _clock.Now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "User", user.Id, null, new { user.Username, Role = role.ToString() }, ct);

        return new UserDto(user.Id, user.Username, user.Role.ToString(), user.IsLocked,
            user.ForcePasswordChange, user.CreatedAt, user.LastLoginAt);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        RequireAdmin();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
                   ?? throw new NotFoundException("المستخدم غير موجود.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.Admin)
            throw new ValidationException2("لا يمكن تعيين دور Admin عبر هذه الشاشة.");

        if (!string.IsNullOrWhiteSpace(request.Username) && request.Username.Trim() != user.Username)
        {
            var exists = await _db.Users.AnyAsync(u => u.Username == request.Username.Trim() && u.Id != id && !u.IsDeleted, ct);
            if (exists)
                throw new ValidationException2("اسم المستخدم مستخدم بالفعل.");
            user.Username = request.Username.Trim();
        }

        var before = new { user.Username, Role = user.Role.ToString(), user.IsLocked };
        user.Role = role;

        if (request.IsLocked.HasValue && request.IsLocked.Value && !user.IsLocked)
        {
            user.IsLocked = true;
            user.LockedUntil = _clock.Now.AddMinutes(15);
            user.CurrentSessionToken = null;
        }
        else if (request.IsLocked.HasValue && !request.IsLocked.Value)
        {
            user.IsLocked = false;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "User", user.Id, before,
            new { user.Username, Role = user.Role.ToString(), user.IsLocked }, ct);

        return new UserDto(user.Id, user.Username, user.Role.ToString(), user.IsLocked,
            user.ForcePasswordChange, user.CreatedAt, user.LastLoginAt);
    }

    public async Task ResetPasswordAsync(AdminResetPasswordRequest request, CancellationToken ct = default)
    {
        RequireAdmin();

        if (request.NewPassword.Length < AuthService.MinPasswordLength)
            throw new ValidationException2($"كلمة المرور يجب ألا تقل عن {AuthService.MinPasswordLength} أحرف.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, ct)
                   ?? throw new NotFoundException("المستخدم غير موجود.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.ForcePasswordChange = true;
        user.CurrentSessionToken = null;
        user.FailedLoginAttempts = 0;
        user.IsLocked = false;
        user.LockedUntil = null;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ResetPassword", "User", user.Id, null, new { user.Username }, ct);
    }

    public async Task<IReadOnlyList<TemporaryPermissionDto>> ListTempPermissionsAsync(CancellationToken ct = default)
    {
        RequireAdmin();
        return await _db.TemporaryPermissions.AsNoTracking()
            .OrderByDescending(tp => tp.Id)
            .Select(tp => new TemporaryPermissionDto(
                tp.Id,
                tp.UserId,
                tp.User!.Username,
                tp.Permission,
                tp.StartsAt,
                tp.EndsAt,
                tp.IsRevoked))
            .ToListAsync(ct);
    }

    public async Task GrantTemporaryPermissionAsync(GrantTemporaryPermissionRequest request, CancellationToken ct = default)
    {
        RequireAdmin();

        if (!PermissionNames.AllAdminPermissions.Contains(request.Permission))
            throw new ValidationException2("صلاحية غير معروفة.");

        if (request.EndsAt <= request.StartsAt)
            throw new ValidationException2("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");

        if (request.StartsAt < _clock.Now)
            throw new ValidationException2("لا يمكن منح صلاحية ببداية في الماضي.");

        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, ct)
                         ?? throw new NotFoundException("المستخدم غير موجود.");

        if (targetUser.Role != UserRole.AssetManager)
            throw new ValidationException2("التفويض المؤقت متاح لمسؤول الأصول فقط.");

        _db.TemporaryPermissions.Add(new TemporaryPermission
        {
            UserId = request.UserId,
            Permission = request.Permission,
            GrantedBy = _current.UserId!.Value,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("GrantTempPermission", "User", request.UserId,
            null, new { UserId = request.UserId, Permission = request.Permission, request.StartsAt, request.EndsAt }, ct);
    }

    public async Task RevokeTemporaryPermissionAsync(int permissionId, CancellationToken ct = default)
    {
        RequireAdmin();

        var tp = await _db.TemporaryPermissions.FirstOrDefaultAsync(x => x.Id == permissionId, ct)
                 ?? throw new NotFoundException("التفويض غير موجود.");

        if (!tp.IsRevoked)
        {
            tp.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("RevokeTempPermission", "User", tp.UserId,
                new { tp.Permission }, new { Revoked = true }, ct);
        }
    }
}
