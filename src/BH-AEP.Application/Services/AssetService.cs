using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IAssetService
{
    Task<PagedResult<AssetListDto>> ListAsync(AssetQuery query, CancellationToken ct = default);
    Task<AssetDetailDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AssetDetailDto> CreateAsync(CreateAssetRequest request, CancellationToken ct = default);
    Task<AssetDetailDto> UpdateAsync(int id, UpdateAssetRequest request, CancellationToken ct = default);
    Task SoftDeleteAsync(int id, CancellationToken ct = default);
    Task SetAccessoriesAsync(int id, IReadOnlyList<AssetAccessoryDto> items, CancellationToken ct = default);
}

public class AssetService : IAssetService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;
    private readonly IAssetCodeGenerator _codeGen;
    private readonly INotificationScanService _scanner;

    public AssetService(
        IAppDbContext db,
        ICurrentUser current,
        IClock clock,
        IAuditService audit,
        IPermissionService permissions,
        IAssetCodeGenerator codeGen,
        INotificationScanService scanner)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _audit = audit;
        _permissions = permissions;
        _codeGen = codeGen;
        _scanner = scanner;
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
            throw new ForbiddenException("دورك لا يسمح بتنفيذ عمليات التعديل على الأصول.");
        await Task.CompletedTask;
    }

    private async Task<bool> CanSeeValueAsync(CancellationToken ct)
        => _current.Role == UserRole.Admin || await _permissions.HasPermissionAsync(PermissionNames.FinancialValue, ct);

    private static HashSet<int> Descendants(int id, IReadOnlyList<(int Id, int? ParentId)> all)
    {
        var set = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var item in all)
            {
                if (item.ParentId == parent && !set.Contains(item.Id))
                {
                    set.Add(item.Id);
                    queue.Enqueue(item.Id);
                }
            }
        }
        return set;
    }

    private async Task<HashSet<int>> CategoryChildIdsAsync(int categoryId, CancellationToken ct)
    {
        var all = await _db.Categories.AsNoTracking()
            .Select(c => new { c.Id, c.ParentId }).ToListAsync(ct);
        return Descendants(categoryId, all.Select(c => (c.Id, c.ParentId)).ToList());
    }

    private async Task<HashSet<int>> DepartmentChildIdsAsync(int deptId, CancellationToken ct)
    {
        var all = await _db.Departments.AsNoTracking()
            .Select(d => new { d.Id, d.ParentId }).ToListAsync(ct);
        return Descendants(deptId, all.Select(d => (d.Id, d.ParentId)).ToList());
    }

    private async Task<HashSet<int>> LocationChildIdsAsync(int locationId, CancellationToken ct)
    {
        var all = await _db.Locations.AsNoTracking()
            .Select(l => new { l.Id, l.ParentId }).ToListAsync(ct);
        return Descendants(locationId, all.Select(l => (l.Id, l.ParentId)).ToList());
    }

    public async Task<PagedResult<AssetListDto>> ListAsync(AssetQuery query, CancellationToken ct = default)
    {
        var q = _db.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped);

        if (query.CategoryId.HasValue)
        {
            var catIds = await CategoryChildIdsAsync(query.CategoryId.Value, ct);
            catIds.Add(query.CategoryId.Value);
            q = q.Where(a => catIds.Contains(a.CategoryId));
        }

        if (query.DepartmentId.HasValue)
        {
            var deptIds = await DepartmentChildIdsAsync(query.DepartmentId.Value, ct);
            deptIds.Add(query.DepartmentId.Value);
            q = q.Where(a => deptIds.Contains(a.DepartmentId));
        }

        if (query.LocationId.HasValue)
        {
            var locIds = await LocationChildIdsAsync(query.LocationId.Value, ct);
            locIds.Add(query.LocationId.Value);
            q = q.Where(a => locIds.Contains(a.LocationId));
        }

        if (!string.IsNullOrWhiteSpace(query.AssetCode))
            q = q.Where(a => a.AssetCode.Contains(query.AssetCode.Trim()));

        if (!string.IsNullOrWhiteSpace(query.SerialNumber))
            q = q.Where(a => a.SerialNumber != null && a.SerialNumber.Contains(query.SerialNumber.Trim()));

        if (!string.IsNullOrWhiteSpace(query.PlateNumber))
            q = q.Where(a => a.Vehicle != null && a.Vehicle.PlateNumber.Contains(query.PlateNumber.Trim()));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(a => a.AssetCode.Contains(s) || a.Name.Contains(s) ||
                             (a.SerialNumber != null && a.SerialNumber.Contains(s)) ||
                             (a.Vehicle != null && (a.Vehicle.PlateNumber.Contains(s) || a.Vehicle.ChassisNumber.Contains(s))));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<AssetStatus>(query.Status, out var status))
            q = q.Where(a => a.Status == status);

        if (!string.IsNullOrWhiteSpace(query.CustodyType) && Enum.TryParse<CustodyType>(query.CustodyType, out var ctype))
            q = q.Where(a => a.CustodyType == ctype);

        if (query.EmployeeId.HasValue)
            q = q.Where(a => a.CustodianId == query.EmployeeId.Value);

        var total = await q.CountAsync(ct);

        var ordered = (query.SortDesc
                ? q.OrderByDescending(a => EF.Property<object>(a, MapSortField(query.SortBy)))
                : q.OrderBy(a => EF.Property<object>(a, MapSortField(query.SortBy))));

        var canSeeValue = await CanSeeValueAsync(ct);

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AssetListDto(
                a.Id, a.AssetCode, a.Name, a.CategoryId,
                a.Category!.Parent != null ? a.Category.Parent.Name : a.Category.Name,
                a.Category.Parent != null ? a.Category.Name : null,
                a.Brand, a.Model, a.SerialNumber,
                a.Status.ToString(),
                a.CustodyType != null ? a.CustodyType.ToString() : null,
                a.Custodian != null ? a.Custodian.FullName : null,
                a.Department!.Name,
                a.Location!.Name,
                canSeeValue ? a.PurchaseValue : null,
                a.Vehicle != null ? a.Vehicle.PlateNumber : null,
                a.IsVehicle,
                a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<AssetListDto>(items, total, query.Page, query.PageSize);
    }

    private static string MapSortField(string? sortBy) => sortBy switch
    {
        nameof(Asset.AssetCode) => nameof(Asset.AssetCode),
        nameof(Asset.Name) => nameof(Asset.Name),
        nameof(Asset.CreatedAt) => nameof(Asset.CreatedAt),
        nameof(Asset.UpdatedAt) => nameof(Asset.UpdatedAt),
        nameof(Asset.Status) => nameof(Asset.Status),
        nameof(Asset.DepartmentId) => nameof(Asset.DepartmentId),
        _ => nameof(Asset.Id)
    };

    public async Task<AssetDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var canSeeValue = await CanSeeValueAsync(ct);

        var asset = await _db.Assets.AsNoTracking()
            .Where(a => a.Id == id && !a.IsDeleted)
            .Select(a => new
            {
                a,
                CategoryName = a.Category!.Name,
                SupplierName = a.Supplier != null ? a.Supplier.Name : null,
                DepartmentName = a.Department!.Name,
                LocationName = a.Location!.Name,
                CustodianName = a.Custodian != null ? a.Custodian.FullName : null
            })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("الأصل غير موجود.");

        var hasImage = await _db.AssetAttachments.AnyAsync(x => x.AssetId == id && x.AttachmentType == AttachmentType.Image, ct);
        var hasOpenCustody = await _db.Custodies.AnyAsync(c => c.AssetId == id && c.ReturnDate == null, ct);

        var missing = AssetCompleteness.EvaluateMissingFields(asset.a, hasImage, hasOpenCustody);

        var attachments = await _db.AssetAttachments.AsNoTracking()
            .Where(x => x.AssetId == id)
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new AttachmentDto(
                x.Id, x.AttachmentType.ToString(), x.OriginalName, x.FileSize, x.MimeType, x.UploadedAt))
            .ToListAsync(ct);

        var accessories = await _db.AssetAccessories.AsNoTracking()
            .Where(x => x.AssetId == id)
            .Select(x => new AssetAccessoryDto(x.AccessoryId, x.Accessory!.Name, x.Quantity, x.Notes))
            .ToListAsync(ct);

        VehicleDetailDto? vehicle = null;
        if (asset.a.IsVehicle)
        {
            var v = await _db.VehicleDetails.AsNoTracking().FirstAsync(x => x.AssetId == id, ct);
            vehicle = new VehicleDetailDto(
                v.Id, v.PlateNumber, v.ChassisNumber, v.EngineNumber, v.ManufactureYear,
                v.Color, v.FuelType, v.LicenseExpiryDate, v.ConditionOnReceipt);
        }

        return new AssetDetailDto(
            asset.a.Id, asset.a.AssetCode, asset.a.Name, asset.a.CategoryId, asset.CategoryName,
            asset.a.Brand, asset.a.Model, asset.a.SerialNumber, asset.a.PurchaseDate,
            canSeeValue ? asset.a.PurchaseValue : null,
            asset.a.SupplierId, asset.SupplierName,
            asset.a.LocationId, asset.LocationName,
            asset.a.DepartmentId, asset.DepartmentName,
            asset.a.CustodianId, asset.CustodianName,
            asset.a.CustodyType, asset.a.Status,
            asset.a.Notes, asset.a.InspectionNumber, asset.a.InspectionDate,
            asset.a.IsVehicle,
            missing.Count > 0, missing.ToArray(),
            vehicle, attachments, accessories);
    }

    public async Task<AssetDetailDto> CreateAsync(CreateAssetRequest request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        await ValidatePlacementAsync(request.CategoryId, request.DepartmentId, request.LocationId, ct);
        ValidateStatus(request.Status);

        if (request.IsVehicle)
        {
            if (request.Vehicle is null)
                throw new ValidationException2("الأصل المعرّف كمركبة يتطلب بيانات المركبة.");
            await ValidateVehicleUniqueAsync(null, request.Vehicle, ct);
        }

        if (request.SupplierId.HasValue)
        {
            var supplierOk = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId.Value && !s.IsDeleted, ct);
            if (!supplierOk)
                throw new ValidationException2("المورد غير موجود.");
        }

        var code = await _codeGen.NextAsync(ct);

        var asset = new Asset
        {
            AssetCode = code,
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            Brand = request.Brand?.Trim(),
            Model = request.Model?.Trim(),
            SerialNumber = request.SerialNumber?.Trim(),
            PurchaseDate = request.PurchaseDate,
            PurchaseValue = request.PurchaseValue,
            SupplierId = request.SupplierId,
            LocationId = request.LocationId,
            DepartmentId = request.DepartmentId,
            Status = request.Status,
            Notes = request.Notes,
            InspectionNumber = request.InspectionNumber,
            InspectionDate = request.InspectionDate,
            IsVehicle = request.IsVehicle,
            CreatedAt = _clock.Now,
            UpdatedAt = _clock.Now,
            IsDeleted = false
        };

        _db.Assets.Add(asset);

        if (request.IsVehicle)
        {
            _db.VehicleDetails.Add(new VehicleDetail
            {
                Asset = asset,
                PlateNumber = request.Vehicle!.PlateNumber,
                ChassisNumber = request.Vehicle.ChassisNumber,
                EngineNumber = request.Vehicle.EngineNumber,
                ManufactureYear = request.Vehicle.ManufactureYear,
                Color = request.Vehicle.Color,
                FuelType = request.Vehicle.FuelType,
                LicenseExpiryDate = request.Vehicle.LicenseExpiryDate,
                ConditionOnReceipt = request.Vehicle.ConditionOnReceipt
            });
        }

        await _db.SaveChangesAsync(ct);

        if (request.CustodianEmployeeId.HasValue)
        {
            var exists = await _db.Employees.AnyAsync(e => e.Id == request.CustodianEmployeeId.Value && !e.IsDeleted, ct);
            if (!exists)
                throw new ValidationException2("الموظف غير موجود.");

            asset.Custodies.Add(new Custody
            {
                AssetId = asset.Id,
                CustodianId = request.CustodianEmployeeId.Value,
                CustodyType = CustodyType.Individual,
                HandoverDate = _clock.Now,
                CreatedBy = RequireUser()
            });
            asset.CustodianId = request.CustodianEmployeeId.Value;
            asset.CustodyType = CustodyType.Individual;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("Create", "Asset", asset.Id, null,
            new { asset.AssetCode, asset.Name, asset.DepartmentId, asset.LocationId, asset.Status }, ct);

        await _scanner.CheckAssetMissingDataAsync(asset.Id, ct);

        return await GetByIdAsync(asset.Id, ct);
    }

    public async Task<AssetDetailDto> UpdateAsync(int id, UpdateAssetRequest request, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        var asset = await _db.Assets
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct)
            ?? throw new NotFoundException("الأصل غير موجود.");

        var before = new
        {
            asset.Name, asset.CategoryId, asset.Brand, asset.Model, asset.SerialNumber,
            asset.PurchaseDate, asset.PurchaseValue, asset.SupplierId, asset.Status, asset.Notes,
            asset.InspectionNumber, asset.InspectionDate
        };

        // الإدارة والموقع حقول مشتقة من آخر نقل/جرد — لا تُعدَّل يدويًا
        if (request.DepartmentId != asset.DepartmentId || request.LocationId != asset.LocationId)
            throw new ValidationException2("لتغيير إدارة الأصل أو موقعه استخدم عملية النقل — هذه الحقول مشتقة تلقائيًا.");

        await ValidatePlacementAsync(request.CategoryId, asset.DepartmentId, asset.LocationId, ct);
        ValidateStatus(request.Status);

        if (request.IsVehicle != asset.IsVehicle)
            throw new ValidationException2("لا يمكن تغيير نوع الأصل (مركبة/عادي) بعد الإنشاء.");

        asset.Name = request.Name.Trim();
        asset.CategoryId = request.CategoryId;
        asset.Brand = request.Brand?.Trim();
        asset.Model = request.Model?.Trim();
        asset.SerialNumber = request.SerialNumber?.Trim();
        asset.PurchaseDate = request.PurchaseDate;
        asset.PurchaseValue = request.PurchaseValue;
        asset.SupplierId = request.SupplierId;
        asset.Status = request.Status;
        asset.Notes = request.Notes;
        asset.InspectionNumber = request.InspectionNumber;
        asset.InspectionDate = request.InspectionDate;
        asset.UpdatedAt = _clock.Now;

        if (request.SupplierId.HasValue)
        {
            var supplierOk = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId.Value && !s.IsDeleted, ct);
            if (!supplierOk)
                throw new ValidationException2("المورد غير موجود.");
        }

        if (request.IsVehicle)
        {
            if (request.Vehicle is null)
                throw new ValidationException2("بيانات المركبة مطلوبة.");
            await ValidateVehicleUniqueAsync(asset.Id, request.Vehicle, ct);

            if (asset.Vehicle is null)
            {
                _db.VehicleDetails.Add(new VehicleDetail
                {
                    AssetId = asset.Id,
                    PlateNumber = request.Vehicle.PlateNumber,
                    ChassisNumber = request.Vehicle.ChassisNumber,
                    EngineNumber = request.Vehicle.EngineNumber,
                    ManufactureYear = request.Vehicle.ManufactureYear,
                    Color = request.Vehicle.Color,
                    FuelType = request.Vehicle.FuelType,
                    LicenseExpiryDate = request.Vehicle.LicenseExpiryDate,
                    ConditionOnReceipt = request.Vehicle.ConditionOnReceipt
                });
            }
            else
            {
                asset.Vehicle.PlateNumber = request.Vehicle.PlateNumber;
                asset.Vehicle.ChassisNumber = request.Vehicle.ChassisNumber;
                asset.Vehicle.EngineNumber = request.Vehicle.EngineNumber;
                asset.Vehicle.ManufactureYear = request.Vehicle.ManufactureYear;
                asset.Vehicle.Color = request.Vehicle.Color;
                asset.Vehicle.FuelType = request.Vehicle.FuelType;
                asset.Vehicle.LicenseExpiryDate = request.Vehicle.LicenseExpiryDate;
                asset.Vehicle.ConditionOnReceipt = request.Vehicle.ConditionOnReceipt;
            }
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Update", "Asset", asset.Id, before,
            new
            {
                asset.Name, asset.CategoryId, asset.Brand, asset.Model, asset.SerialNumber,
                asset.PurchaseDate, asset.PurchaseValue, asset.SupplierId, asset.Status, asset.Notes,
                asset.InspectionNumber, asset.InspectionDate
            }, ct);

        await _scanner.CheckAssetMissingDataAsync(asset.Id, ct);

        return await GetByIdAsync(asset.Id, ct);
    }

    public async Task SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        var asset = await _db.Assets
            .Include(a => a.Custodies)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct)
            ?? throw new NotFoundException("الأصل غير موجود.");

        if (asset.Status == AssetStatus.Scrapped)
            throw new ValidationException2("الأصل مُخرَّد — لا يمكن حذفه كتصحيح لخطأ إدخال.");

        if (asset.Custodies.Any(c => c.ReturnDate == null))
            throw new ValidationException2("الأصل لديه عهدة مفتوحة — أغلق العهدة قبل الحذف.");

        asset.IsDeleted = true;
        asset.UpdatedAt = _clock.Now;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SoftDelete", "Asset", asset.Id,
            new { asset.AssetCode, asset.Name }, new { IsDeleted = true }, ct);
    }

    public async Task SetAccessoriesAsync(int id, IReadOnlyList<AssetAccessoryDto> items, CancellationToken ct = default)
    {
        await RequireWriteRoleAsync(ct);

        var exists = await _db.Assets.AnyAsync(a => a.Id == id && !a.IsDeleted, ct);
        if (!exists)
            throw new NotFoundException("الأصل غير موجود.");

        var existing = await _db.AssetAccessories.Where(x => x.AssetId == id).ToListAsync(ct);
        _db.AssetAccessories.RemoveRange(existing);

        foreach (var item in items)
        {
            var accessoryOk = await _db.Accessories.AnyAsync(a => a.Id == item.AccessoryId && !a.IsDeleted, ct);
            if (!accessoryOk)
                throw new ValidationException2("المشتمل غير موجود.");

            _db.AssetAccessories.Add(new AssetAccessory
            {
                AssetId = id,
                AccessoryId = item.AccessoryId,
                Quantity = item.Quantity,
                Notes = item.Notes
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SetAccessories", "Asset", id, null, new { Count = items.Count }, ct);
    }

    private async Task ValidatePlacementAsync(int categoryId, int departmentId, int locationId, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && !c.IsDeleted, ct)
                       ?? throw new ValidationException2("التصنيف غير موجود.");

        if (category.ParentId is null)
            throw new ValidationException2("يجب اختيار تصنيف فرعي (وليس تصنيفًا رئيسيًا).");

        var hasSubChildren = await _db.Categories.AnyAsync(c => c.ParentId == categoryId && !c.IsDeleted, ct);
        if (hasSubChildren)
            throw new ValidationException2("يجب اختيار تصنيف بمستوى الورقة الأخيرة.");

        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId && !d.IsDeleted, ct)
                   ?? throw new ValidationException2("الإدارة غير موجودة.");
        var deptHasChildren = await _db.Departments.AnyAsync(d => d.ParentId == departmentId && !d.IsDeleted, ct);
        if (deptHasChildren)
            throw new ValidationException2("يجب اختيار إدارة بمستوى الورقة الأخيرة (وليس قطاعًا).");

        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId && !l.IsDeleted, ct)
                       ?? throw new ValidationException2("الموقع غير موجود.");
        var locHasChildren = await _db.Locations.AnyAsync(l => l.ParentId == locationId && !l.IsDeleted, ct);
        if (locHasChildren)
            throw new ValidationException2("يجب اختيار موقع بمستوى الورقة الأخيرة.");

        _ = dept;
    }

    private static void ValidateStatus(AssetStatus status)
    {
        if (status == AssetStatus.Scrapped)
            throw new ValidationException2("لا يمكن إنشاء أصل بحالة «مُخرَّد» مباشرة — استخدم طلب التخريد.");
        if (status == AssetStatus.Missing)
            throw new ValidationException2("لا يمكن تعيين حالة «مفقود» يدويًا — تأتي من تسوية الجرد.");
    }

    private async Task ValidateVehicleUniqueAsync(int? assetId, VehicleDetailRequest v, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(v.PlateNumber))
            throw new ValidationException2("رقم اللوحة مطلوب للمركبة.");
        if (string.IsNullOrWhiteSpace(v.ChassisNumber))
            throw new ValidationException2("رقم الشاسيه مطلوب للمركبة.");

        var plateDup = await _db.VehicleDetails.AnyAsync(x => x.PlateNumber == v.PlateNumber && x.AssetId != assetId, ct);
        if (plateDup)
            throw new ValidationException2("رقم اللوحة مستخدم لمركبة أخرى.");

        var chassisDup = await _db.VehicleDetails.AnyAsync(x => x.ChassisNumber == v.ChassisNumber && x.AssetId != assetId, ct);
        if (chassisDup)
            throw new ValidationException2("رقم الشاسيه مستخدم لمركبة أخرى.");
    }
}
