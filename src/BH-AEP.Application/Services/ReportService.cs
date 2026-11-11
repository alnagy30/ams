using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IReportService
{
    Task<ReportTableListDto> TablesAsync(CancellationToken ct = default);
    Task<ReportResponse> RunAsync(ReportRequest request, CancellationToken ct = default);
}

public class ReportService : IReportService
{
    private readonly IAppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUser _current;

    public ReportService(IAppDbContext db, IPermissionService permissions, ICurrentUser current)
    {
        _db = db;
        _permissions = permissions;
        _current = current;
    }

    private static readonly ReportFieldDto[] AssetFields =
    {
        new("AssetCode", "كود الأصل", "string", false),
        new("Name", "اسم الأصل", "string", false),
        new("MainCategory", "التصنيف الرئيسي", "string", false),
        new("SubCategory", "التصنيف الفرعي", "string", false),
        new("Brand", "الماركة", "string", false),
        new("Model", "الموديل", "string", false),
        new("SerialNumber", "السيريال", "string", false),
        new("Status", "الحالة", "string", false),
        new("Department", "الإدارة", "string", false),
        new("Location", "الموقع", "string", false),
        new("CustodyType", "نوع العهدة", "string", false),
        new("Custodian", "حامل العهدة", "string", false),
        new("Supplier", "المورد", "string", false),
        new("PurchaseDate", "تاريخ الشراء", "date", false),
        new("PurchaseValue", "قيمة الشراء", "number", true),
        new("PlateNumber", "رقم اللوحة", "string", false),
        new("ChassisNumber", "رقم الشاسيه", "string", false),
        new("InspectionNumber", "رقم الفحص", "string", false),
        new("IsVehicle", "مركبة؟", "string", false),
        new("CreatedAt", "تاريخ الإضافة", "date", false)
    };

    private static readonly ReportFieldDto[] EmployeeFields =
    {
        new("FullName", "الاسم", "string", false),
        new("NationalId", "الرقم القومي", "string", false),
        new("JobTitle", "الوظيفة", "string", false),
        new("Department", "الإدارة", "string", false),
        new("HeldAssets", "عدد الأصول تحت العهدة", "number", false),
        new("CreatedAt", "تاريخ التسجيل", "date", false)
    };

    public Task<ReportTableListDto> TablesAsync(CancellationToken ct = default)
    {
        var tables = new List<ReportTableDefinitionDto>
        {
            new("Assets", "الأصول", AssetFields),
            new("Employees", "الموظفون", EmployeeFields)
        };
        return Task.FromResult(new ReportTableListDto(tables));
    }

    public async Task<ReportResponse> RunAsync(ReportRequest request, CancellationToken ct = default)
    {
        var canSeeValue = _current.Role == UserRole.Admin ||
                          await _permissions.HasPermissionAsync(PermissionNames.FinancialValue, ct);

        var columns = ResolveColumns(request.Table, request.Columns, canSeeValue);

        return request.Table switch
        {
            "Assets" => await RunAssetsAsync(request, columns, canSeeValue, ct),
            "Employees" => await RunEmployeesAsync(request, columns, ct),
            _ => throw new ValidationException2("جدول تقرير غير مدعوم.")
        };
    }

    private static List<ReportFieldDto> ResolveColumns(string table, IReadOnlyList<string> requested, bool canSeeValue)
    {
        var available = (table == "Assets" ? AssetFields : EmployeeFields).ToList();
        if (!canSeeValue)
            available = available.Where(f => !f.Financial).ToList();

        var names = available.Select(f => f.Key).ToHashSet();
        var selected = new List<ReportFieldDto>();
        var all = requested == null || requested.Count == 0;

        foreach (var field in available)
        {
            if (all || (requested.Contains(field.Key) && names.Contains(field.Key)))
                selected.Add(field);
        }

        if (selected.Count == 0)
            throw new ValidationException2("اختر عمودًا واحدًا على الأقل للعرض.");

        return selected;
    }

    private static string? FirstFilter(Dictionary<string, string> filters, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (filters != null && filters.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    private async Task<ReportResponse> RunAssetsAsync(ReportRequest request, List<ReportFieldDto> columns,
        bool canSeeValue, CancellationToken ct)
    {
        var includeScrapped = request.IncludeScrapped;

        var q = _db.Assets.AsNoTracking().Where(a => !a.IsDeleted);
        if (!includeScrapped)
            q = q.Where(a => a.Status != AssetStatus.Scrapped);

        var filters = request.Filters ?? new Dictionary<string, string>();

        if (FirstFilter(filters, "Search") is { Length: > 0 } search)
            q = q.Where(a => a.AssetCode.Contains(search) || a.Name.Contains(search) ||
                             (a.SerialNumber != null && a.SerialNumber.Contains(search)));

        if (FirstFilter(filters, "Status") is { Length: > 0 } status && Enum.TryParse<AssetStatus>(status, out var st))
            q = q.Where(a => a.Status == st);

        if (FirstFilter(filters, "DepartmentId") is { } deptStr && int.TryParse(deptStr, out var deptId))
            q = q.Where(a => a.DepartmentId == deptId);

        if (FirstFilter(filters, "CategoryId") is { } catStr && int.TryParse(catStr, out var catId))
            q = q.Where(a => a.CategoryId == catId);

        if (FirstFilter(filters, "LocationId") is { } locStr && int.TryParse(locStr, out var locId))
            q = q.Where(a => a.LocationId == locId);

        if (FirstFilter(filters, "PlateNumber") is { } plate)
            q = q.Where(a => a.Vehicle != null && a.Vehicle.PlateNumber.Contains(plate));

        if (FirstFilter(filters, "CustodyType") is { } ctStr && Enum.TryParse<CustodyType>(ctStr, out var cType))
            q = q.Where(a => a.CustodyType == cType);

        var rows = await q
            .OrderBy(a => a.AssetCode)
            .Select(a => new
            {
                a.AssetCode, a.Name,
                MainCategory = a.Category!.Parent != null ? a.Category.Parent.Name : a.Category.Name,
                SubCategory = a.Category.Parent != null ? a.Category.Name : "",
                a.Brand, a.Model, a.SerialNumber,
                Status = a.Status.ToString(),
                Department = a.Department!.Name,
                Location = a.Location!.Name,
                CustodyType = a.CustodyType != null ? a.CustodyType.ToString() : "",
                Custodian = a.Custodian != null ? a.Custodian.FullName : "",
                Supplier = a.Supplier != null ? a.Supplier.Name : "",
                a.PurchaseDate, a.PurchaseValue,
                PlateNumber = a.Vehicle != null ? a.Vehicle.PlateNumber : "",
                ChassisNumber = a.Vehicle != null ? a.Vehicle.ChassisNumber : "",
                a.InspectionNumber,
                IsVehicle = a.IsVehicle ? "نعم" : "لا",
                a.CreatedAt
            })
            .ToListAsync(ct);

        var resultRows = new List<ReportRowDto>();
        foreach (var r in rows)
        {
            var values = new List<object?>();
            foreach (var col in columns)
            {
                values.Add(col.Key switch
                {
                    "AssetCode" => r.AssetCode,
                    "Name" => r.Name,
                    "MainCategory" => r.MainCategory,
                    "SubCategory" => r.SubCategory,
                    "Brand" => r.Brand,
                    "Model" => r.Model,
                    "SerialNumber" => r.SerialNumber,
                    "Status" => r.Status,
                    "Department" => r.Department,
                    "Location" => r.Location,
                    "CustodyType" => r.CustodyType,
                    "Custodian" => r.Custodian,
                    "Supplier" => r.Supplier,
                    "PurchaseDate" => r.PurchaseDate,
                    "PurchaseValue" => canSeeValue ? r.PurchaseValue : null,
                    "PlateNumber" => r.PlateNumber,
                    "ChassisNumber" => r.ChassisNumber,
                    "InspectionNumber" => r.InspectionNumber,
                    "IsVehicle" => r.IsVehicle,
                    "CreatedAt" => r.CreatedAt,
                    _ => null
                });
            }
            resultRows.Add(new ReportRowDto(values));
        }

        return new ReportResponse(request.Table, columns, resultRows);
    }

    private async Task<ReportResponse> RunEmployeesAsync(ReportRequest request, List<ReportFieldDto> columns,
        CancellationToken ct)
    {
        var q = _db.Employees.AsNoTracking().Where(e => !e.IsDeleted);

        var filters = request.Filters ?? new Dictionary<string, string>();

        if (FirstFilter(filters, "DepartmentId") is { } deptStr && int.TryParse(deptStr, out var deptId))
            q = q.Where(e => e.DepartmentId == deptId);

        if (FirstFilter(filters, "Search") is { Length: > 0 } search)
            q = q.Where(e => e.FullName.Contains(search) || e.NationalId.Contains(search));

        var rows = await q
            .OrderBy(e => e.FullName)
            .Select(e => new
            {
                e.FullName, e.NationalId, e.JobTitle,
                Department = e.Department!.Name,
                HeldAssets = _db.Custodies.Count(c => c.CustodianId == e.Id && c.ReturnDate == null),
                e.CreatedAt
            })
            .ToListAsync(ct);

        var resultRows = new List<ReportRowDto>();
        foreach (var r in rows)
        {
            var values = new List<object?>();
            foreach (var col in columns)
            {
                values.Add(col.Key switch
                {
                    "FullName" => r.FullName,
                    "NationalId" => r.NationalId,
                    "JobTitle" => r.JobTitle,
                    "Department" => r.Department,
                    "HeldAssets" => r.HeldAssets,
                    "CreatedAt" => r.CreatedAt,
                    _ => null
                });
            }
            resultRows.Add(new ReportRowDto(values));
        }

        return new ReportResponse(request.Table, columns, resultRows);
    }
}
