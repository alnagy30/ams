using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IScrapService
{
    Task<ScrapRequestDto> CreateAsync(ScrapRequestCreate request, CancellationToken ct = default);
    Task<ScrapRequestDto> DecideAsync(ScrapRequestDecision decision, CancellationToken ct = default);
    Task<PagedResult<ScrapRequestDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<ScrapRequestDto>> ScrappedAssetsHistoryAsync(CancellationToken ct = default);
}

public class ScrapService : IScrapService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IPermissionService _permissions;

    public ScrapService(IAppDbContext db, ICurrentUser current, IClock clock,
        IAuditService audit, INotificationService notifications, IPermissionService permissions)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
        _permissions = permissions;
    }

    private int RequireUser()
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            throw new UnauthorizedException();
        return _current.UserId.Value;
    }

    private async Task RequireWriteRoleAsync(CancellationToken ct)
    {
        if (_current.Role is not (UserRole.Admin or UserRole.AssetManager))
            throw new ForbiddenException("دورك لا يسمح بتنفيذ عمليات التخريد.");
        await Task.CompletedTask;
    }

    public async Task<ScrapRequestDto> CreateAsync(ScrapRequestCreate request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId && !a.IsDeleted, ct)
                    ?? throw new NotFoundException("الأصل غير موجود.");

        if (asset.Status is AssetStatus.Scrapped or AssetStatus.Missing)
            throw new ValidationException2("الأصل بحالة لا تسمح بطلب التخريد.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException2("سبب التخريد مطلوب.");

        var existsPending = await _db.ScrapRequests.AnyAsync(
            s => s.AssetId == request.AssetId && s.Status == ScrapRequestStatus.Pending, ct);
        if (existsPending)
            throw new ValidationException2("يوجد طلب تخريد قيد الانتظار لهذا الأصل.");

        var scrap = new ScrapRequest
        {
            AssetId = request.AssetId,
            Reason = request.Reason.Trim(),
            Status = ScrapRequestStatus.Pending,
            CreatedBy = RequireUser(),
            CreatedAt = _clock.Now
        };

        _db.ScrapRequests.Add(scrap);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Create", "ScrapRequest", scrap.Id, null,
            new { AssetId = asset.Id, asset.AssetCode, request.Reason }, ct);

        await _notifications.CreateAsync(NotificationType.Generic, "Asset", asset.Id,
            $"طلب تخريد جديد للأصل {asset.AssetCode}.", ct);

        return await GetDtoAsync(scrap.Id, ct);
    }

    public async Task<ScrapRequestDto> DecideAsync(ScrapRequestDecision decision, CancellationToken ct = default)
    {
        if (_current.Role != UserRole.Admin &&
            !await _permissions.HasPermissionAsync(PermissionNames.ScrapHistory, ct))
            throw new ForbiddenException("اعتماد طلبات التخريد متاح للادمن فقط (أو عبر تفويض مؤقت).");

        var scrap = await _db.ScrapRequests.FirstOrDefaultAsync(s => s.Id == decision.Id, ct)
                    ?? throw new NotFoundException("طلب التخريد غير موجود.");

        if (scrap.Status != ScrapRequestStatus.Pending)
            throw new ValidationException2("الطلب سبق البت فيه.");

        if (decision.Approve)
        {
            scrap.Status = ScrapRequestStatus.Approved;
            scrap.Decision = decision.DecisionText ?? "معتمد";
            scrap.ApprovedBy = RequireUser();
            scrap.ApprovedAt = _clock.Now;

            var asset = await _db.Assets.Include(a => a.Custodies)
                .FirstOrDefaultAsync(a => a.Id == scrap.AssetId, ct)
                ?? throw new NotFoundException("الأصل غير موجود.");

            // إغلاق أي عهدة مفتوحة عند التخريد
            var open = asset.Custodies.FirstOrDefault(c => c.ReturnDate == null);
            if (open is not null)
                open.ReturnDate = _clock.Now;

            asset.Status = AssetStatus.Scrapped;
            asset.CustodianId = null;
            asset.CustodyType = null;
            asset.UpdatedAt = _clock.Now;
        }
        else
        {
            scrap.Status = ScrapRequestStatus.Rejected;
            scrap.Decision = decision.DecisionText ?? "مرفوض";
            scrap.ApprovedBy = RequireUser();
            scrap.ApprovedAt = _clock.Now;
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(decision.Approve ? "ApproveScrap" : "RejectScrap", "Asset", scrap.AssetId,
            new { ScrapId = scrap.Id }, new { decision.DecisionText, scrap.Status }, ct);

        if (decision.Approve)
        {
            await _notifications.CreateAsync(NotificationType.Generic, "Asset", scrap.AssetId,
                $"تمت الموافقة على تخريد الأصل وانتقاله لسجل التخريد.", ct);
        }

        return await GetDtoAsync(scrap.Id, ct);
    }

    public async Task<PagedResult<ScrapRequestDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ScrapRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ScrapRequestStatus>(status, out var st))
            q = q.Where(s => s.Status == st);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ScrapRequestDto(
                s.Id, s.AssetId, s.Asset!.AssetCode, s.Asset.Name,
                s.Reason, s.Decision, s.Status.ToString(), s.CreatedAt,
                _db.Users.Where(u => u.Id == s.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                s.ApprovedAt,
                s.ApprovedBy.HasValue ? _db.Users.Where(u => u.Id == s.ApprovedBy).Select(u => u.Username).FirstOrDefault() : null))
            .ToListAsync(ct);

        return new PagedResult<ScrapRequestDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<ScrapRequestDto>> ScrappedAssetsHistoryAsync(CancellationToken ct = default)
    {
        // الأصول المُخرَّدة تظهر فقط في هذا السجل التاريخي
        return await _db.ScrapRequests.AsNoTracking()
            .Where(s => s.Status == ScrapRequestStatus.Approved)
            .OrderByDescending(s => s.ApprovedAt)
            .Select(s => new ScrapRequestDto(
                s.Id, s.AssetId, s.Asset!.AssetCode, s.Asset.Name,
                s.Reason, s.Decision, s.Status.ToString(), s.CreatedAt,
                _db.Users.Where(u => u.Id == s.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                s.ApprovedAt,
                s.ApprovedBy.HasValue ? _db.Users.Where(u => u.Id == s.ApprovedBy).Select(u => u.Username).FirstOrDefault() : null))
            .ToListAsync(ct);
    }

    private async Task<ScrapRequestDto> GetDtoAsync(int scrapId, CancellationToken ct)
    {
        var dto = await _db.ScrapRequests.AsNoTracking()
            .Where(s => s.Id == scrapId)
            .Select(s => new ScrapRequestDto(
                s.Id, s.AssetId, s.Asset!.AssetCode, s.Asset.Name,
                s.Reason, s.Decision, s.Status.ToString(), s.CreatedAt,
                _db.Users.Where(u => u.Id == s.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                s.ApprovedAt,
                s.ApprovedBy.HasValue ? _db.Users.Where(u => u.Id == s.ApprovedBy).Select(u => u.Username).FirstOrDefault() : null))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("طلب التخريد غير موجود.");
        return dto;
    }
}
