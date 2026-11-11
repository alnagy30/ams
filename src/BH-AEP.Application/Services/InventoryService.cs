using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryCampaignDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CampaignProgressDto> GetCampaignAsync(int campaignId, CancellationToken ct = default);
    Task<IReadOnlyList<AssetListDto>> GetQueueAsync(int campaignId, CancellationToken ct = default);
    Task<InventoryCampaignDto> CreateAsync(CreateInventoryCampaignRequest request, CancellationToken ct = default);
    Task<InventoryCampaignDto> UpdateAsync(int id, UpdateInventoryCampaignRequest request, CancellationToken ct = default);
    Task<InventoryCampaignDto> StartAsync(int id, CancellationToken ct = default);
    Task<InventoryCampaignDto> CompleteAsync(int id, CancellationToken ct = default);
    Task CancelAsync(int id, CancellationToken ct = default);
    Task RecordItemAsync(int campaignId, InventoryExecuteRequest request, CancellationToken ct = default);
    Task ResolveMismatchAsync(int campaignId, int assetId, string decision, CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IPermissionService _permissions;

    public InventoryService(IAppDbContext db, ICurrentUser current, IClock clock,
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
            throw new ForbiddenException("دورك لا يسمح بتنفيذ عمليات الجرد.");
        await Task.CompletedTask;
    }

    private async Task<HashSet<int>> DepartmentSubtreeAsync(int departmentId, CancellationToken ct)
    {
        var all = await _db.Departments.AsNoTracking().Select(d => new { d.Id, d.ParentId }).ToListAsync(ct);
        var set = new HashSet<int> { departmentId };
        bool changed;
        do
        {
            changed = false;
            foreach (var d in all)
            {
                if (d.ParentId.HasValue && set.Contains(d.ParentId.Value) && set.Add(d.Id))
                    changed = true;
            }
        } while (changed);
        return set;
    }

    private async Task<IReadOnlyList<int>> TargetAssetIdsAsync(InventoryCampaign campaign, CancellationToken ct)
    {
        HashSet<int>? deptIds = null;
        if (campaign.DepartmentId.HasValue)
            deptIds = await DepartmentSubtreeAsync(campaign.DepartmentId.Value, ct);

        return await _db.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped &&
                        (deptIds == null || deptIds.Contains(a.DepartmentId)))
            .Select(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<InventoryCampaignDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.InventoryCampaigns.AsNoTracking();
        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id, c.Name, c.DepartmentId, DeptName = c.Department != null ? c.Department.Name : null,
                c.StartDate, c.EndDate, c.Status, c.CreatedAt, c.CreatedBy,
                FoundCount = c.Items.Count(i => i.IsFound),
                NotFoundCount = c.Items.Count(i => !i.IsFound)
            })
            .ToListAsync(ct);

        var items = new List<InventoryCampaignDto>();
        foreach (var r in rows)
        {
            int targetCount;
            if (r.DepartmentId.HasValue)
            {
                var ids = await DepartmentSubtreeAsync(r.DepartmentId.Value, ct);
                targetCount = await _db.Assets.CountAsync(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped && ids.Contains(a.DepartmentId), ct);
            }
            else
            {
                targetCount = await _db.Assets.CountAsync(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped, ct);
            }

            items.Add(new InventoryCampaignDto(
                r.Id, r.Name, r.DepartmentId, r.DeptName, r.StartDate, r.EndDate,
                r.Status.ToString(), targetCount, r.FoundCount, r.NotFoundCount, r.CreatedAt,
                _db.Users.Where(u => u.Id == r.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? ""));
        }

        return new PagedResult<InventoryCampaignDto>(items, total, page, pageSize);
    }

    public async Task<CampaignProgressDto> GetCampaignAsync(int campaignId, CancellationToken ct = default)
    {
        var campaign = await _db.InventoryCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct)
            ?? throw new NotFoundException("حملة الجرد غير موجودة.");

        var targetIds = (await TargetAssetIdsAsync(campaign, ct)).ToHashSet();

        var items = new List<InventoryItemDto>();
        foreach (var i in campaign.Items.OrderByDescending(i => i.RecordedAt))
        {
            var name = await _db.Assets.AsNoTracking()
                .Where(a => a.Id == i.AssetId)
                .Select(a => new { a.AssetCode, a.Name }).FirstOrDefaultAsync(ct);
            if (name is null) continue;

            items.Add(new InventoryItemDto(
                i.AssetId, name.AssetCode, name.Name, i.IsFound, i.Notes, i.RecordedAt,
                _db.Users.Where(u => u.Id == i.RecordedBy).Select(u => u.Username).FirstOrDefault()));
        }

        var unrecordedAssets = new List<InventoryItemDto>();
        var recordedIds = campaign.Items.Select(i => i.AssetId).ToHashSet();
        var pending = await _db.Assets.AsNoTracking()
            .Where(a => targetIds.Contains(a.Id) && !recordedIds.Contains(a.Id))
            .OrderBy(a => a.AssetCode)
            .Select(a => new { a.Id, a.AssetCode, a.Name })
            .ToListAsync(ct);
        foreach (var p in pending)
            unrecordedAssets.Add(new InventoryItemDto(p.Id, p.AssetCode, p.Name, null, null, null, null));

        items.AddRange(unrecordedAssets);

        return new CampaignProgressDto(
            campaign.Id, campaign.Name, items, targetIds.Count,
            campaign.Items.Count, campaign.Items.Count(i => !i.IsFound));
    }

    public async Task<IReadOnlyList<AssetListDto>> GetQueueAsync(int campaignId, CancellationToken ct = default)
    {
        var campaign = await _db.InventoryCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct)
                       ?? throw new NotFoundException("حملة الجرد غير موجودة.");

        var recordedIds = await _db.InventoryItems.Where(i => i.CampaignId == campaignId)
            .Select(i => i.AssetId).ToListAsync(ct);

        var targetIds = (await TargetAssetIdsAsync(campaign, ct)).ToHashSet();

        var assets = _db.Assets
            .Where(a => targetIds.Contains(a.Id) && !recordedIds.Contains(a.Id))
            .OrderBy(a => a.AssetCode)
            .Select(a => new AssetListDto(
                a.Id, a.AssetCode, a.Name, a.CategoryId,
                a.Category!.Parent != null ? a.Category.Parent.Name : a.Category.Name,
                a.Category.Parent != null ? a.Category.Name : null,
                a.Brand, a.Model, a.SerialNumber, a.Status.ToString(),
                a.CustodyType != null ? a.CustodyType.ToString() : null,
                a.Custodian != null ? a.Custodian.FullName : null,
                a.Department!.Name, a.Location!.Name, null,
                a.Vehicle != null ? a.Vehicle.PlateNumber : null, a.IsVehicle, a.CreatedAt, a.UpdatedAt));

        return await assets.ToListAsync(ct);
    }

    public async Task<InventoryCampaignDto> CreateAsync(CreateInventoryCampaignRequest request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException2("اسم الحملة مطلوب.");

        if (request.EndDate < request.StartDate)
            throw new ValidationException2("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");

        if (request.DepartmentId.HasValue)
        {
            var exists = await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId.Value && !d.IsDeleted, ct);
            if (!exists)
                throw new ValidationException2("الإدارة غير موجودة.");
        }

        var campaign = new InventoryCampaign
        {
            Name = request.Name.Trim(),
            DepartmentId = request.DepartmentId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = InventoryCampaignStatus.Planned,
            CreatedAt = _clock.Now,
            CreatedBy = RequireUser()
        };

        _db.InventoryCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "InventoryCampaign", campaign.Id, null,
            new { campaign.Name, campaign.DepartmentId, campaign.StartDate, campaign.EndDate }, ct);

        return await GetDtoAsync(campaign.Id, ct);
    }

    public async Task<InventoryCampaignDto> UpdateAsync(int id, UpdateInventoryCampaignRequest request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);
        var campaign = await _db.InventoryCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("حملة الجرد غير موجودة.");

        if (campaign.Status == InventoryCampaignStatus.Completed)
            throw new ValidationException2("لا يمكن تعديل حملة منتهية.");

        if (request.Name is not null)
            campaign.Name = request.Name.Trim();
        if (request.StartDate.HasValue)
            campaign.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue)
            campaign.EndDate = request.EndDate.Value;

        await _db.SaveChangesAsync(ct);
        return await GetDtoAsync(campaign.Id, ct);
    }

    public async Task<InventoryCampaignDto> StartAsync(int id, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);
        var campaign = await _db.InventoryCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("حملة الجرد غير موجودة.");

        if (campaign.Status == InventoryCampaignStatus.Completed)
            throw new ValidationException2("الحملة منتهية بالفعل.");

        campaign.Status = InventoryCampaignStatus.InProgress;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Start", "InventoryCampaign", id, null, null, ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task<InventoryCampaignDto> CompleteAsync(int id, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);
        var campaign = await _db.InventoryCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("حملة الجرد غير موجودة.");

        campaign.Status = InventoryCampaignStatus.Completed;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Complete", "InventoryCampaign", id, null, null, ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task CancelAsync(int id, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);
        var campaign = await _db.InventoryCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("حملة الجرد غير موجودة.");

        if (campaign.Status == InventoryCampaignStatus.Completed)
            throw new ValidationException2("لا يمكن إلغاء حملة منتهية.");

        campaign.Status = InventoryCampaignStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Cancel", "InventoryCampaign", id, null, null, ct);
    }

    public async Task RecordItemAsync(int campaignId, InventoryExecuteRequest request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);
        var campaign = await _db.InventoryCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct)
                       ?? throw new NotFoundException("حملة الجرد غير موجودة.");

        if (campaign.Status == InventoryCampaignStatus.Completed)
            throw new ValidationException2("الحملة منتهية.");

        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId && !a.IsDeleted, ct)
                    ?? throw new NotFoundException("الأصل غير موجود.");

        var existing = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.CampaignId == campaignId && i.AssetId == request.AssetId, ct);

        if (existing is null)
        {
            existing = new InventoryItem { CampaignId = campaignId, AssetId = request.AssetId };
            _db.InventoryItems.Add(existing);
        }

        var previous = new { existing.IsFound, existing.Notes };
        existing.IsFound = request.IsFound;
        existing.Notes = request.Notes;
        existing.RecordedBy = RequireUser();
        existing.RecordedAt = _clock.Now;

        await _db.SaveChangesAsync(ct);

        if (!request.IsFound && !(previous.IsFound == false))
        {
            await _notifications.CreateAsync(NotificationType.InventoryMismatch, "Asset", request.AssetId,
                $"الأصل {asset.AssetCode} غير موجود أثناء جرد حملة «{campaign.Name}» — يرجى المراجعة والتسوية.", ct);
        }

        await _audit.LogAsync("RecordInventoryItem", "Asset", request.AssetId,
            previous, new { existing.IsFound, existing.Notes, CampaignId = campaignId }, ct);
    }

    public async Task ResolveMismatchAsync(int campaignId, int assetId, string decision, CancellationToken ct = default)
    {
        if (_current.Role != UserRole.Admin &&
            !await _permissions.HasPermissionAsync(PermissionNames.InventoryHistory, ct))
            throw new ForbiddenException("تسوية الجرد متاحة للادمن أو عبر تفويض مؤقت.");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.CampaignId == campaignId && i.AssetId == assetId, ct)
            ?? throw new NotFoundException("السجل غير موجود.");

        var asset = await _db.Assets.Include(a => a.Custodies)
            .FirstOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new NotFoundException("الأصل غير موجود.");

        switch (decision.ToLowerInvariant())
        {
            case "realmissing":
                item.Notes = (item.Notes is null ? "" : item.Notes + " ") + "تسوية: الأصل مفقود فعلًا.";
                asset.Status = AssetStatus.Missing;
                var open = asset.Custodies.FirstOrDefault(c => c.ReturnDate == null);
                if (open is not null)
                    open.ReturnDate = _clock.Now;
                asset.CustodianId = null;
                asset.CustodyType = null;
                asset.UpdatedAt = _clock.Now;
                break;

            case "mistake":
                item.Notes = (item.Notes is null ? "" : item.Notes + " ") + "تسوية: خطأ (الأصل موجود).";
                item.IsFound = true;
                break;

            case "reviewing":
                item.Notes = (item.Notes is null ? "" : item.Notes + " ") + "تسوية: لا يزال قيد المراجعة.";
                break;

            default:
                throw new ValidationException2("قرار غير معروف.");
        }

        var notification = await _db.Notifications
            .Where(n => n.Type == NotificationType.InventoryMismatch && n.Status == NotificationStatus.Open &&
                        n.RelatedEntityId == assetId)
            .OrderByDescending(n => n.Id)
            .FirstOrDefaultAsync(ct);
        if (notification is not null)
        {
            notification.Status = NotificationStatus.Resolved;
            notification.ResolvedBy = RequireUser();
            notification.ResolvedAt = _clock.Now;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ResolveInventoryMismatch", "Asset", assetId,
            null, new { decision, asset.Status }, ct);
    }

    private async Task<InventoryCampaignDto> GetDtoAsync(int campaignId, CancellationToken ct)
    {
        var page = await ListAsync(1, int.MaxValue, ct);
        return page.Items.FirstOrDefault(x => x.Id == campaignId)
               ?? throw new NotFoundException("حملة الجرد غير موجودة.");
    }
}
