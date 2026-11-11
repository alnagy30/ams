using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface INotificationService
{
    Task<int> CreateAsync(NotificationType type, string relatedEntityType, int relatedEntityId,
        string message, CancellationToken ct = default);
    Task<PagedResult<NotificationDto>> ListAsync(NotificationListQuery query, CancellationToken ct = default);
    Task<NotificationDto> ResolveAsync(int id, CancellationToken ct = default);
    Task<int> CountOpenAsync(CancellationToken ct = default);
}

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;

    public NotificationService(IAppDbContext db, ICurrentUser current, IClock clock)
    {
        _db = db;
        _current = current;
        _clock = clock;
    }

    public async Task<int> CreateAsync(NotificationType type, string relatedEntityType, int relatedEntityId,
        string message, CancellationToken ct = default)
    {
        var n = new Notification
        {
            Type = type,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            Message = message,
            Status = NotificationStatus.Open,
            CreatedAt = _clock.Now
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);
        return n.Id;
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(NotificationListQuery query, CancellationToken ct = default)
    {
        var q = _db.Notifications.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var parsed = Enum.TryParse<NotificationType>(query.Type, out var type);
            if (parsed)
                q = q.Where(n => n.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<NotificationStatus>(query.Status, out var status))
                q = q.Where(n => n.Status == status);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type.ToString(),
                n.RelatedEntityType,
                n.RelatedEntityId,
                n.Message,
                n.Status.ToString(),
                n.CreatedAt,
                n.ResolvedBy,
                _db.Users.Where(u => u.Id == n.ResolvedBy).Select(u => u.Username).FirstOrDefault(),
                n.ResolvedAt))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, total, query.Page, query.PageSize);
    }

    public async Task<NotificationDto> ResolveAsync(int id, CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            throw new UnauthorizedException();

        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("التنبيه غير موجود.");

        if (n.Status == NotificationStatus.Resolved)
            return ToDto(n, _current.UserId.Value, _current.Role?.ToString() ?? "");

        n.Status = NotificationStatus.Resolved;
        n.ResolvedBy = _current.UserId;
        n.ResolvedAt = _clock.Now;

        await _db.SaveChangesAsync(ct);
        return ToDto(n, _current.UserId.Value, _current.Role?.ToString() ?? "");
    }

    public async Task<int> CountOpenAsync(CancellationToken ct = default)
    {
        return await _db.Notifications.CountAsync(n => n.Status == NotificationStatus.Open, ct);
    }

    private static NotificationDto ToDto(Notification n, int resolvedBy, string resolvedByName)
    {
        return new NotificationDto(
            n.Id, n.Type.ToString(), n.RelatedEntityType, n.RelatedEntityId, n.Message,
            n.Status.ToString(), n.CreatedAt, resolvedBy, resolvedByName, n.ResolvedAt);
    }
}
