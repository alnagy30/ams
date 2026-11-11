using BH_AEP.Application.Common;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionsForUserAsync(int userId, UserRole role, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);
    Task RequirePermissionAsync(string permission, CancellationToken ct = default);
    Task RequireAnyWriteRoleAsync(CancellationToken ct = default);
    bool CanSeeFinancialValue { get; }
}

public class PermissionService : IPermissionService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;

    public PermissionService(IAppDbContext db, ICurrentUser current, IClock clock)
    {
        _db = db;
        _current = current;
        _clock = clock;
    }

    public bool CanSeeFinancialValue => _current.Role == UserRole.Admin;

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated)
            return Array.Empty<string>();

        if (_current.Role == UserRole.Admin)
            return PermissionNames.AllAdminPermissions;

        var userId = _current.UserId!.Value;
        return await GetGrantedForUserAsync(userId, ct);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsForUserAsync(int userId, UserRole role, CancellationToken ct = default)
    {
        if (role == UserRole.Admin)
            return PermissionNames.AllAdminPermissions;

        if (role == UserRole.Viewer)
            return Array.Empty<string>();

        return await GetGrantedForUserAsync(userId, ct);
    }

    private async Task<IReadOnlyList<string>> GetGrantedForUserAsync(int userId, CancellationToken ct)
    {
        var now = _clock.Now;

        return await _db.TemporaryPermissions
            .Where(tp => tp.UserId == userId && !tp.IsRevoked && tp.StartsAt <= now && tp.EndsAt >= now)
            .Select(tp => tp.Permission)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated)
            return false;

        if (_current.Role == UserRole.Admin)
            return true;

        if (_current.Role == UserRole.Viewer)
            return false;

        var userId = _current.UserId!.Value;
        var now = _clock.Now;

        return await _db.TemporaryPermissions.AnyAsync(
            tp => tp.UserId == userId && tp.Permission == permission &&
                  !tp.IsRevoked && tp.StartsAt <= now && tp.EndsAt >= now, ct);
    }

    public async Task RequirePermissionAsync(string permission, CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(permission, ct))
            throw new ForbiddenException();
    }

    public async Task RequireAnyWriteRoleAsync(CancellationToken ct = default)
    {
        if (_current.Role is not (UserRole.Admin or UserRole.AssetManager))
            throw new ForbiddenException("دورك لا يسمح بتنفيذ عمليات تعديل.");
        await Task.CompletedTask;
    }
}
