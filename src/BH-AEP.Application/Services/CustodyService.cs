using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface ICustodyService
{
    Task<CustodyRecordDto> AssignAsync(int assetId, int? employeeId, CustodyType type, DateTime date, string? notes, CancellationToken ct = default);
    Task<int> AssignManyAsync(int employeeId, IReadOnlyList<int> assetIds, DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<UnassignedAssetDto>> ListUnassignedAsync(int? departmentId, CancellationToken ct = default);
    Task<PagedResult<CustodyRecordDto>> ListAsync(CustodyListQuery query, CancellationToken ct = default);
    Task<TransferRecordDto> TransferSingleAsync(SingleTransferRequest request, CancellationToken ct = default);
    Task<BulkTransferPlanDto> PlanBulkAsync(IReadOnlyList<int> assetIds, CancellationToken ct = default);
    Task<int> BulkTransferAsync(BulkTransferRequest request, CancellationToken ct = default);
    Task<int> ReturnToStoreAsync(ReturnToStoreRequest request, CancellationToken ct = default);
    Task<PagedResult<TransferRecordDto>> HistoryAsync(TransferRecordQuery query, CancellationToken ct = default);
    Task TransferToEmployeeAsync(int assetId, int employeeId, DateTime date, string? notes, CancellationToken ct = default);
}

public class CustodyService : ICustodyService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IPermissionService _permissions;

    public CustodyService(IAppDbContext db, ICurrentUser current, IClock clock,
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
            throw new ForbiddenException("دورك لا يسمح بتنفيذ هذه العملية.");
        await Task.CompletedTask;
    }

    private async Task RequireBulkPermissionAsync(CancellationToken ct)
    {
        if (_current.Role == UserRole.Admin)
            return;
        if (!await _permissions.HasPermissionAsync(PermissionNames.BulkTransfer, ct))
            throw new ForbiddenException("النقل الجماعي متاح للادمن أو عبر تفويض مؤقت.");
    }

    private static void EnsureTransferable(Asset asset)
    {
        if (asset.Status is AssetStatus.Scrapped or AssetStatus.Missing)
            throw new ValidationException2($"الأصل {asset.AssetCode} بحالة لا تسمح بمنح عهدة أو نقل.");
    }

    private async Task<Asset> LoadAssetAsync(int assetId, CancellationToken ct)
    {
        var asset = await _db.Assets
            .Include(a => a.Custodies)
            .FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted, ct)
            ?? throw new NotFoundException("الأصل غير موجود.");
        return asset;
    }

    private async Task LoadEmployeeAsync(int employeeId, CancellationToken ct)
    {
        var ok = await _db.Employees.AnyAsync(e => e.Id == employeeId && !e.IsDeleted, ct);
        if (!ok)
            throw new ValidationException2("الموظف غير موجود أو موقوف.");
    }

    /// <summary>إغلاق العهدة المفتوحة الحالية وفتح عهدة جديدة للأصل (بدون حفظ).</summary>
    private void ApplyCustodyChange(Asset asset, int? employeeId, CustodyType type, DateTime date, string? notes)
    {
        EnsureTransferable(asset);

        if (type == CustodyType.Individual)
        {
            if (employeeId is null)
                throw new ValidationException2("العهدة الفردية تتطلب اختيار موظف.");
        }
        else
        {
            employeeId = null;
        }

        var open = asset.Custodies.FirstOrDefault(c => c.ReturnDate == null);
        if (open is not null && open.CustodianId == employeeId && open.CustodyType == type)
            throw new ValidationException2("الأصل على نفس هذه العهدة بالفعل.");

        if (open is not null)
            open.ReturnDate = date;

        asset.Custodies.Add(new Custody
        {
            AssetId = asset.Id,
            CustodianId = employeeId,
            CustodyType = type,
            HandoverDate = date,
            Notes = notes,
            CreatedBy = RequireUser()
        });

        asset.CustodianId = employeeId;
        asset.CustodyType = type;
        asset.UpdatedAt = _clock.Now;
    }

    public async Task<CustodyRecordDto> AssignAsync(int assetId, int? employeeId, CustodyType type,
        DateTime date, string? notes, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        var asset = await LoadAssetAsync(assetId, ct);

        var hasOpen = asset.Custodies.Any(c => c.ReturnDate == null);
        if (hasOpen)
            throw new ValidationException2("الأصل لديه عهدة مفتوحة — استخدم النقل أو الإرجاع للمخزن.");

        if (type == CustodyType.Individual)
            await LoadEmployeeAsync(employeeId!.Value, ct);

        ApplyCustodyChange(asset, employeeId, type, date, notes);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("AssignCustody", "Asset", asset.Id,
            new { EmployeeId = employeeId, Type = type.ToString(), date },
            new { asset.CustodianId, asset.CustodyType }, ct);

        await _notifications.CreateAsync(NotificationType.Generic, "Custody", asset.Id,
            $"تم منح عهدة للأصل {asset.AssetCode} ({(type == CustodyType.Individual ? "عهدة فردية" : "إدارية عامة")}).", ct);

        return await BuildCustodyDtoAsync(asset.Id, ct);
    }

    public async Task<int> AssignManyAsync(int employeeId, IReadOnlyList<int> assetIds,
        DateTime date, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);
        await LoadEmployeeAsync(employeeId, ct);

        var assets = await _db.Assets
            .Where(a => assetIds.Contains(a.Id) && !a.IsDeleted)
            .Include(a => a.Custodies)
            .ToListAsync(ct);

        var valid = new List<Asset>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var asset in assets)
        {
            EnsureTransferable(asset);
            if (asset.Custodies.Any(c => c.ReturnDate == null))
                continue;

            ApplyCustodyChange(asset, employeeId, CustodyType.Individual, date, "تعيين جماعي للعهدة");
            valid.Add(asset);
        }

        if (valid.Count == 0)
            throw new ValidationException2("لم توجد أصول صالحة للتعيين (قد تكون جميعها على عهدة مفتوحة).");

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("BulkAssignCustody", "Employee", employeeId, null,
            new { EmployeeId = employeeId, AssetCount = valid.Count, date }, ct);

        return valid.Count;
    }

    public async Task<IReadOnlyList<UnassignedAssetDto>> ListUnassignedAsync(int? departmentId, CancellationToken ct = default)
    {
        var q = _db.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped &&
                        !_db.Custodies.Any(c => c.AssetId == a.Id && c.ReturnDate == null));

        if (departmentId.HasValue)
            q = q.Where(a => a.DepartmentId == departmentId.Value);

        return await q
            .OrderBy(a => a.AssetCode)
            .Select(a => new UnassignedAssetDto(a.Id, a.AssetCode, a.Name, a.Department!.Name))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<CustodyRecordDto>> ListAsync(CustodyListQuery query, CancellationToken ct = default)
    {
        var q = _db.Custodies.AsNoTracking().AsQueryable();

        if (query.AssetId.HasValue)
            q = q.Where(c => c.AssetId == query.AssetId.Value);

        if (query.EmployeeId.HasValue)
            q = q.Where(c => c.CustodianId == query.EmployeeId.Value);

        if (query.DepartmentId.HasValue)
            q = q.Where(c => c.Asset!.DepartmentId == query.DepartmentId.Value);

        if (query.OpenOnly)
            q = q.Where(c => c.ReturnDate == null);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(c => c.HandoverDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CustodyRecordDto(
                c.Id, c.AssetId, c.Asset!.AssetCode, c.Asset.Name,
                c.CustodianId, c.Custodian!.FullName, c.CustodyType.ToString(),
                c.HandoverDate, c.ReturnDate, c.Notes,
                _db.Users.Where(u => u.Id == c.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                c.ReturnDate == null))
            .ToListAsync(ct);

        return new PagedResult<CustodyRecordDto>(items, total, query.Page, query.PageSize);
    }

    public async Task TransferToEmployeeAsync(int assetId, int employeeId, DateTime date,
        string? notes, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);
        var asset = await LoadAssetAsync(assetId, ct);
        EnsureTransferable(asset);
        await LoadEmployeeAsync(employeeId, ct);

        ApplyCustodyChange(asset, employeeId, CustodyType.Individual, date, notes);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TransferRecordDto> TransferSingleAsync(SingleTransferRequest request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        var asset = await LoadAssetAsync(request.AssetId, ct);
        EnsureTransferable(asset);

        var toDept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == request.ToDepartmentId && !d.IsDeleted, ct)
                     ?? throw new ValidationException2("الإدارة الوجهة غير موجودة.");
        var toLoc = await _db.Locations.FirstOrDefaultAsync(l => l.Id == request.ToLocationId && !l.IsDeleted, ct)
                    ?? throw new ValidationException2("الموقع الوجهة غير موجود.");

        var beforeDept = asset.DepartmentId;
        var beforeLoc = asset.LocationId;
        var beforeCustodian = asset.CustodianId;
        var beforeCustodyType = asset.CustodyType;

        var hasIndividualCustody = asset.CustodyType == CustodyType.Individual && asset.CustodianId != null;

        int? employeeId = null;
        CustodyType newCustodyType;
        if (hasIndividualCustody)
        {
            if (request.RecipientEmployeeId is null)
                throw new ValidationException2("الأصل بعهدة فردية — يجب اختيار الموظف المستلم.");
            await LoadEmployeeAsync(request.RecipientEmployeeId.Value, ct);
            employeeId = request.RecipientEmployeeId.Value;
            newCustodyType = CustodyType.Individual;
        }
        else if (request.RecipientEmployeeId.HasValue)
        {
            await LoadEmployeeAsync(request.RecipientEmployeeId.Value, ct);
            employeeId = request.RecipientEmployeeId.Value;
            newCustodyType = CustodyType.Individual;
        }
        else
        {
            newCustodyType = CustodyType.DepartmentGeneral;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.Transfers.Add(new Transfer
        {
            AssetId = asset.Id,
            FromDepartmentId = asset.DepartmentId,
            ToDepartmentId = toDept.Id,
            FromLocationId = asset.LocationId,
            ToLocationId = toLoc.Id,
            TransferDate = request.TransferDate,
            Reason = request.Reason,
            CreatedBy = RequireUser(),
            IsBulk = false
        });

        // إقفال العهدة القديمة وفتح عهدة جديدة في نفس المعاملة
        ApplyCustodyChange(asset, employeeId, newCustodyType, request.TransferDate, request.Reason);

        asset.DepartmentId = toDept.Id;
        asset.LocationId = toLoc.Id;
        asset.UpdatedAt = _clock.Now;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("Transfer", "Asset", asset.Id,
            new { DepartmentId = beforeDept, LocationId = beforeLoc, CustodianId = beforeCustodian, CustodyType = beforeCustodyType },
            new { asset.DepartmentId, asset.LocationId, asset.CustodianId, asset.CustodyType }, ct);

        await _notifications.CreateAsync(NotificationType.Generic, "Transfer", asset.Id,
            $"تم نقل الأصل {asset.AssetCode} إلى إدارة جديدة.", ct);

        return await BuildTransferDtoAsync(asset.Id, ct);
    }

    public async Task<BulkTransferPlanDto> PlanBulkAsync(IReadOnlyList<int> assetIds, CancellationToken ct = default)
    {
        await RequireBulkPermissionAsync(ct);

        var assets = await _db.Assets.AsNoTracking()
            .Where(a => assetIds.Contains(a.Id) && !a.IsDeleted)
            .Select(a => new
            {
                a.Id, a.AssetCode, a.Name, a.DepartmentId, a.LocationId, a.Status,
                a.CategoryId, a.Brand, a.Model, a.SerialNumber, a.CustodyType, a.CustodianId,
                a.IsVehicle, a.CreatedAt, a.UpdatedAt, a.PurchaseValue,
                DeptName = a.Department!.Name,
                LocName = a.Location!.Name,
                CatName = a.Category!.Name,
                SubCatName = a.Category.Parent != null ? a.Category.Parent.Name : null,
                CustName = a.Custodian!.FullName,
                Plate = a.Vehicle != null ? a.Vehicle.PlateNumber : null
            })
            .ToListAsync(ct);

        var individual = new List<int>();
        var general = new List<int>();
        var assetDtos = new List<AssetListDto>();

        foreach (var a in assets)
        {
            if (a.Status is AssetStatus.Scrapped or AssetStatus.Missing)
                throw new ValidationException2($"الأصل {a.AssetCode} غير قابل للنقل.");

            assetDtos.Add(new AssetListDto(
                a.Id, a.AssetCode, a.Name, a.CategoryId, a.CatName ?? "", a.SubCatName,
                a.Brand, a.Model, a.SerialNumber, a.Status.ToString(),
                a.CustodyType?.ToString(), a.CustName, a.DeptName, a.LocName,
                a.PurchaseValue, a.Plate, a.IsVehicle, a.CreatedAt, a.UpdatedAt));

            if (a.CustodyType == CustodyType.Individual && a.CustodianId != null)
                individual.Add(a.Id);
            else
                general.Add(a.Id);
        }

        return new BulkTransferPlanDto(individual, general, assetDtos);
    }

    public async Task<int> BulkTransferAsync(BulkTransferRequest request, CancellationToken ct = default)
    {
        await RequireBulkPermissionAsync(ct);

        if (request.AssetIds.Count == 0)
            throw new ValidationException2("لم يتم اختيار أي أصول.");

        var assets = await _db.Assets
            .Where(a => request.AssetIds.Contains(a.Id) && !a.IsDeleted)
            .Include(a => a.Custodies)
            .ToListAsync(ct);

        if (assets.Count != request.AssetIds.Count)
            throw new ValidationException2("بعض الأصول المختارة غير موجودة أو محذوفة.");

        var individualAssets = assets
            .Where(a => a.CustodyType == CustodyType.Individual && a.CustodianId != null)
            .ToList();

        if (individualAssets.Count > 0 && request.RecipientEmployeeId is null)
            throw new ValidationException2(
                $"يوجد {individualAssets.Count} أصل بعهدة فردية — يجب تحديد الموظف المستلم لهم.");

        int? recipientId = request.RecipientEmployeeId;
        if (recipientId.HasValue)
            await LoadEmployeeAsync(recipientId.Value, ct);

        var toDept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == request.ToDepartmentId && !d.IsDeleted, ct)
                     ?? throw new ValidationException2("الإدارة الوجهة غير موجودة.");
        var toLoc = await _db.Locations.FirstOrDefaultAsync(l => l.Id == request.ToLocationId && !l.IsDeleted, ct)
                    ?? throw new ValidationException2("الموقع الوجهة غير موجود.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var asset in assets)
        {
            EnsureTransferable(asset);

            _db.Transfers.Add(new Transfer
            {
                AssetId = asset.Id,
                FromDepartmentId = asset.DepartmentId,
                ToDepartmentId = toDept.Id,
                FromLocationId = asset.LocationId,
                ToLocationId = toLoc.Id,
                TransferDate = request.TransferDate,
                Reason = request.Reason,
                CreatedBy = RequireUser(),
                IsBulk = true
            });

            var isIndividual = individualAssets.Contains(asset);
            var custodianId = isIndividual ? recipientId : null;
            var custodyType = isIndividual ? CustodyType.Individual : CustodyType.DepartmentGeneral;

            ApplyCustodyChange(asset, custodianId, custodyType, request.TransferDate, request.Reason);

            asset.DepartmentId = toDept.Id;
            asset.LocationId = toLoc.Id;
            asset.UpdatedAt = _clock.Now;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("BulkTransfer", "Asset", null, null,
            new { AssetCount = assets.Count, ToDepartmentId = toDept.Id, ToLocationId = toLoc.Id, request.TransferDate }, ct);

        return assets.Count;
    }

    public async Task<int> ReturnToStoreAsync(ReturnToStoreRequest request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        var assets = await _db.Assets
            .Where(a => request.AssetIds.Contains(a.Id) && !a.IsDeleted)
            .Include(a => a.Custodies)
            .ToListAsync(ct);

        if (assets.Count == 0)
            throw new ValidationException2("لم يتم العثور على أصول صالحة.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var asset in assets)
        {
            var open = asset.Custodies.FirstOrDefault(c => c.ReturnDate == null);
            if (open is not null)
            {
                open.ReturnDate = request.Date;
                if (!string.IsNullOrWhiteSpace(request.Notes))
                    open.Notes = request.Notes;
            }

            asset.Status = AssetStatus.InStorage;
            asset.CustodianId = null;
            asset.CustodyType = null;
            asset.UpdatedAt = _clock.Now;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _audit.LogAsync("ReturnToStore", "Asset", null, null,
            new { AssetCount = assets.Count, request.Date }, ct);

        return assets.Count;
    }

    public async Task<PagedResult<TransferRecordDto>> HistoryAsync(TransferRecordQuery query, CancellationToken ct = default)
    {
        var q = _db.Transfers.AsNoTracking().AsQueryable();

        if (query.DepartmentId.HasValue)
            q = q.Where(t => t.FromDepartmentId == query.DepartmentId.Value || t.ToDepartmentId == query.DepartmentId.Value);

        if (query.AssetId.HasValue)
            q = q.Where(t => t.AssetId == query.AssetId.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(t => t.TransferDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TransferRecordDto(
                t.Id, t.AssetId, t.Asset!.AssetCode, t.Asset.Name,
                t.FromDepartmentId, _db.Departments.Where(d => d.Id == t.FromDepartmentId).Select(d => d.Name).FirstOrDefault(),
                t.ToDepartmentId, _db.Departments.Where(d => d.Id == t.ToDepartmentId).Select(d => d.Name).FirstOrDefault(),
                t.FromLocationId, _db.Locations.Where(l => l.Id == t.FromLocationId).Select(l => l.Name).FirstOrDefault(),
                t.ToLocationId, _db.Locations.Where(l => l.Id == t.ToLocationId).Select(l => l.Name).FirstOrDefault(),
                t.TransferDate, t.Reason,
                _db.Users.Where(u => u.Id == t.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                t.IsBulk))
            .ToListAsync(ct);

        return new PagedResult<TransferRecordDto>(items, total, query.Page, query.PageSize);
    }

    private async Task<CustodyRecordDto> BuildCustodyDtoAsync(int assetId, CancellationToken ct)
    {
        var dto = await _db.Custodies.AsNoTracking()
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Id)
            .Select(x => new CustodyRecordDto(
                x.Id, x.AssetId, x.Asset!.AssetCode, x.Asset.Name,
                x.CustodianId, x.Custodian!.FullName, x.CustodyType.ToString(),
                x.HandoverDate, x.ReturnDate, x.Notes,
                _db.Users.Where(u => u.Id == x.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                x.ReturnDate == null))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("العهدة غير موجودة.");
        return dto;
    }

    private async Task<TransferRecordDto> BuildTransferDtoAsync(int assetId, CancellationToken ct)
    {
        var dto = await _db.Transfers.AsNoTracking()
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.Id)
            .Select(x => new TransferRecordDto(
                x.Id, x.AssetId, x.Asset!.AssetCode, x.Asset.Name,
                x.FromDepartmentId, _db.Departments.Where(d => d.Id == x.FromDepartmentId).Select(d => d.Name).FirstOrDefault(),
                x.ToDepartmentId, _db.Departments.Where(d => d.Id == x.ToDepartmentId).Select(d => d.Name).FirstOrDefault(),
                x.FromLocationId, _db.Locations.Where(l => l.Id == x.FromLocationId).Select(l => l.Name).FirstOrDefault(),
                x.ToLocationId, _db.Locations.Where(l => l.Id == x.ToLocationId).Select(l => l.Name).FirstOrDefault(),
                x.TransferDate, x.Reason,
                _db.Users.Where(u => u.Id == x.CreatedBy).Select(u => u.Username).FirstOrDefault() ?? "",
                x.IsBulk))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("النقل غير موجود.");
        return dto;
    }
}
