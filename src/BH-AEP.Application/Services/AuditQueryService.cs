using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogDto>> ForEntityAsync(string entityType, int entityId, CancellationToken ct = default);
    Task<PagedResult<OrgChangeDto>> OrgHistoryAsync(int page, int pageSize, CancellationToken ct = default);
}

public class AuditQueryService : IAuditQueryService
{
    private readonly IAppDbContext _db;

    public AuditQueryService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(x => x.Action.Contains(query.Action.Trim()));

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(x => x.EntityType == query.EntityType.Trim());

        if (!string.IsNullOrWhiteSpace(query.UserName))
            q = q.Where(x => _db.Users.Any(u => u.Id == x.UserId && u.Username.Contains(query.UserName.Trim())));

        if (query.From.HasValue)
            q = q.Where(x => x.Timestamp >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(x => x.Timestamp <= query.To.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.Timestamp)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new AuditLogDto(
                x.Id, x.UserId,
                _db.Users.Where(u => u.Id == x.UserId).Select(u => u.Username).FirstOrDefault() ?? "",
                x.Action, x.EntityType, x.EntityId,
                x.BeforeData, x.AfterData, x.Timestamp, x.IpAddress))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto>(items, total, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<AuditLogDto>> ForEntityAsync(string entityType, int entityId, CancellationToken ct = default)
    {
        return await _db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new AuditLogDto(
                x.Id, x.UserId,
                _db.Users.Where(u => u.Id == x.UserId).Select(u => u.Username).FirstOrDefault() ?? "",
                x.Action, x.EntityType, x.EntityId,
                x.BeforeData, x.AfterData, x.Timestamp, x.IpAddress))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<OrgChangeDto>> OrgHistoryAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _db.OrgStructureHistory.CountAsync(ct);

        var items = await _db.OrgStructureHistory.AsNoTracking()
            .OrderByDescending(x => x.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OrgChangeDto(
                x.Id, x.DepartmentId,
                _db.Departments.Where(d => d.Id == x.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? "",
                x.OldParentId,
                _db.Departments.Where(d => d.Id == x.OldParentId).Select(d => d.Name).FirstOrDefault(),
                x.NewParentId,
                _db.Departments.Where(d => d.Id == x.NewParentId).Select(d => d.Name).FirstOrDefault(),
                _db.Users.Where(u => u.Id == x.ChangedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                x.ChangedAt))
            .ToListAsync(ct);

        return new PagedResult<OrgChangeDto>(items, total, page, pageSize);
    }
}
