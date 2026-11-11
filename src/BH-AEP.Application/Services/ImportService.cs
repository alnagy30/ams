using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IImportService
{
    Task<ParseResultDto> ParseAsync(Stream file, string fileName, CancellationToken ct = default);
    Task<ImportSummaryDto> ExecuteAsync(IReadOnlyList<ImportedAssetRow> rows, string? fileName, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatchDto>> BatchesAsync(CancellationToken ct = default);
}

public class ImportService : IImportService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAssetCodeGenerator _codeGen;
    private readonly IExcelImportParser _parser;
    private readonly IAuditService _audit;

    public ImportService(IAppDbContext db, ICurrentUser current, IClock clock,
        IAssetCodeGenerator codeGen, IExcelImportParser parser, IAuditService audit)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _codeGen = codeGen;
        _parser = parser;
        _audit = audit;
    }

    private int RequireUser()
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            throw new UnauthorizedException();
        return _current.UserId.Value;
    }

    public async Task<ParseResultDto> ParseAsync(Stream file, string fileName, CancellationToken ct = default)
    {
        if (_current.Role != Domain.Enums.UserRole.Admin)
            throw new ForbiddenException("الاستيراد متاح للادمن فقط.");
        return await _parser.ParseAsync(file, fileName, ct);
    }

    public async Task<ImportSummaryDto> ExecuteAsync(IReadOnlyList<ImportedAssetRow> rows, string? fileName, CancellationToken ct = default)
    {
        if (_current.Role != UserRole.Admin)
            throw new ForbiddenException("الاستيراد متاح للادمن فقط.");

        var issues = new List<ImportIssueDto>();
        var createdAssets = new List<ImportedAssetRefDto>();
        int success = 0;

        // تحميل القوائم المرجعية مرة واحدة
        var categories = await _db.Categories.AsNoTracking().ToListAsync(ct);
        var departments = await _db.Departments.AsNoTracking().ToListAsync(ct);
        var locations = await _db.Locations.AsNoTracking().ToListAsync(ct);
        var suppliers = (await _db.Suppliers.AsNoTracking().ToListAsync(ct))
            .ToDictionary(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(row.Error))
                    throw new ValidationException2(row.Error);

                var code = await _codeGen.NextAsync(ct);

                var category = ResolveCategory(categories, row.MainCategory, row.SubCategory, row.RowNumber);
                var department = ResolveLeaf(departments.Select(d => new LookupRef(d.Id, d.Name, d.ParentId)).ToList(),
                    row.DepartmentName, row.RowNumber, "الإدارة");
                var location = ResolveLeaf(locations.Select(l => new LookupRef(l.Id, l.Name, l.ParentId)).ToList(),
                    row.LocationName, row.RowNumber, "الموقع");

                int? supplierId = null;
                if (!string.IsNullOrWhiteSpace(row.SupplierName))
                {
                    var supplierName = row.SupplierName.Trim();
                    if (suppliers.TryGetValue(supplierName, out var existing))
                    {
                        supplierId = existing.Id;
                    }
                    else
                    {
                        var created = new Supplier { Name = supplierName };
                        _db.Suppliers.Add(created);
                        await _db.SaveChangesAsync(ct);
                        suppliers[supplierName] = created;
                        supplierId = created.Id;
                    }
                }

                var asset = new Asset
                {
                    AssetCode = code,
                    Name = string.IsNullOrWhiteSpace(row.Name) ? row.SubCategory : row.Name.Trim(),
                    CategoryId = category.Id,
                    Brand = row.Brand?.Trim(),
                    Model = row.Model?.Trim(),
                    SerialNumber = row.SerialNumber?.Trim(),
                    PurchaseDate = row.PurchaseDate,
                    PurchaseValue = row.PurchaseValue,
                    SupplierId = supplierId,
                    LocationId = location.Id,
                    DepartmentId = department.Id,
                    Status = AssetStatus.Active,
                    IsVehicle = row.IsVehicle,
                    CreatedAt = _clock.Now,
                    UpdatedAt = _clock.Now
                };

                _db.Assets.Add(asset);

                if (row.IsVehicle)
                {
                    if (string.IsNullOrWhiteSpace(row.PlateNumber) || string.IsNullOrWhiteSpace(row.ChassisNumber))
                        throw new ValidationException2("بيانات المركبة غير مكتملة (اللوحة/الشاسيه).");
                    _db.VehicleDetails.Add(new VehicleDetail
                    {
                        Asset = asset,
                        PlateNumber = row.PlateNumber,
                        ChassisNumber = row.ChassisNumber,
                        EngineNumber = row.EngineNumber,
                        LicenseExpiryDate = row.LicenseExpiryDate,
                        ConditionOnReceipt = "جديد"
                    });
                }

                await _db.SaveChangesAsync(ct);
                success++;
                createdAssets.Add(new ImportedAssetRefDto(asset.Id, asset.AssetCode, asset.Name));
            }
            catch (Exception ex)
            {
                var message = ex is AppException ? ex.Message : "فشل غير متوقع أثناء الاستيراد.";
                issues.Add(new ImportIssueDto(row.RowNumber, row.AssetCode ?? "", message));
            }
        }

        if (success > 0 || issues.Count > 0)
        {
            _db.ImportBatches.Add(new ImportBatch
            {
                FileName = string.IsNullOrWhiteSpace(fileName) ? $"استيراد-{_clock.Now:yyyyMMddHHmmss}.xlsx" : fileName!.Trim(),
                TotalRows = rows.Count,
                SuccessRows = success,
                FailedRows = issues.Count,
                ErrorLog = System.Text.Json.JsonSerializer.Serialize(issues),
                ImportedAt = _clock.Now,
                ImportedBy = RequireUser()
            });
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("Import", "ImportBatch", null, null,
            new { Total = rows.Count, Success = success, Failed = issues.Count }, ct);

        return new ImportSummaryDto(rows.Count, success, issues.Count, issues, createdAssets);
    }

    private sealed record LookupRef(int Id, string Name, int? ParentId);

    private static Domain.Entities.Category ResolveCategory(
        List<Domain.Entities.Category> all, string? mainName, string? subName, string rowNumber)
    {
        if (string.IsNullOrWhiteSpace(mainName))
            throw new ValidationException2("التصنيف الرئيسي مطلوب.");
        if (string.IsNullOrWhiteSpace(subName))
            throw new ValidationException2("التصنيف الفرعي مطلوب.");

        var main = all.FirstOrDefault(c => c.ParentId is null &&
                                           string.Equals(c.Name.Trim(), mainName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (main is null)
            throw new ValidationException2($"التصنيف الرئيسي «{mainName}» غير موجود في البيانات المرجعية.");

        var sub = all.FirstOrDefault(c => c.ParentId == main.Id &&
                                          string.Equals(c.Name.Trim(), subName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (sub is null)
            throw new ValidationException2($"التصنيف الفرعي «{subName}» غير موجود تحت التصنيف «{mainName}».");

        return sub;
    }

    private static LookupRef ResolveLeaf(IReadOnlyList<LookupRef> all, string? name, string rowNumber, string label)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException2($"{label} مطلوب.");

        var leaf = all.Where(x => string.Equals(x.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var realLeaves = leaf.Where(x => !all.Any(c => c.ParentId == x.Id)).ToList();

        if (realLeaves.Count == 0)
            throw new ValidationException2($"{label} «{name}» غير موجودة.");

        if (realLeaves.Count > 1)
            throw new ValidationException2($"{label} «{name}» مكررة في أكثر من قطاع — حدد الإدارة الكاملة.");

        return realLeaves[0];
    }

    public async Task<IReadOnlyList<ImportBatchDto>> BatchesAsync(CancellationToken ct = default)
    {
        return await _db.ImportBatches.AsNoTracking()
            .OrderByDescending(b => b.Id)
            .Select(b => new ImportBatchDto(
                b.Id, b.FileName, b.TotalRows, b.SuccessRows, b.FailedRows, b.ImportedAt,
                _db.Users.Where(u => u.Id == b.ImportedBy).Select(u => u.Username).FirstOrDefault() ?? ""))
            .ToListAsync(ct);
    }
}
