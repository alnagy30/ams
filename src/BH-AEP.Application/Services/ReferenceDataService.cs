using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IReferenceDataService
{
    Task<LookupsDto> GetLookupsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TreeNodeDto>> CategoryTreeAsync(CancellationToken ct = default);
    Task<TreeNodeDto> CreateCategoryAsync(TreeNodeRequest request, CancellationToken ct = default);
    Task<TreeNodeDto> UpdateCategoryAsync(int id, TreeNodeRequest request, CancellationToken ct = default);
    Task DeleteCategoryAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<TreeNodeDto>> DepartmentTreeAsync(CancellationToken ct = default);
    Task<TreeNodeDto> CreateDepartmentAsync(TreeNodeRequest request, CancellationToken ct = default);
    Task<TreeNodeDto> UpdateDepartmentAsync(int id, TreeNodeRequest request, CancellationToken ct = default);
    Task DeleteDepartmentAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<TreeNodeDto>> LocationTreeAsync(CancellationToken ct = default);
    Task<TreeNodeDto> CreateLocationAsync(TreeNodeRequest request, CancellationToken ct = default);
    Task<TreeNodeDto> UpdateLocationAsync(int id, TreeNodeRequest request, CancellationToken ct = default);
    Task DeleteLocationAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierDto>> SuppliersAsync(CancellationToken ct = default);
    Task<SupplierDto> CreateSupplierAsync(SupplierRequest request, CancellationToken ct = default);
    Task<SupplierDto> UpdateSupplierAsync(int id, SupplierRequest request, CancellationToken ct = default);
    Task DeleteSupplierAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<AccessoryDto>> AccessoriesAsync(CancellationToken ct = default);
    Task<AccessoryDto> CreateAccessoryAsync(AccessoryRequest request, CancellationToken ct = default);
    Task<AccessoryDto> UpdateAccessoryAsync(int id, AccessoryRequest request, CancellationToken ct = default);
    Task DeleteAccessoryAsync(int id, CancellationToken ct = default);
}

public class ReferenceDataService : IReferenceDataService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;

    public ReferenceDataService(IAppDbContext db, ICurrentUser current, IClock clock,
        IAuditService audit, IPermissionService permissions)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _audit = audit;
        _permissions = permissions;
    }

    private int RequireUser()
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            throw new UnauthorizedException();
        return _current.UserId.Value;
    }

    private async Task RequireReferenceAdminAsync(CancellationToken ct)
    {
        if (_current.Role != UserRole.Admin &&
            !await _permissions.HasPermissionAsync(PermissionNames.ReferenceData, ct))
            throw new ForbiddenException("البيانات المرجعية متاحة للادمن أو عبر تفويض مؤقت.");
    }

    private static string Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException2("الاسم مطلوب.");
        return name.Trim();
    }

    private sealed record TreeItem(int Id, string Name, int? ParentId);

    private static void ValidateTreeEdit(IReadOnlyList<TreeItem> all, int? selfId, string name, int? newParentId)
    {
        if (newParentId is not null && !all.Any(t => t.Id == newParentId))
            throw new ValidationException2("العنصر الأب غير موجود.");

        // منع عمل الدورة (نقل تحت نفسها أو تحت أحفادها)
        if (selfId is not null && newParentId is not null && newParentId.Value == selfId)
            throw new ValidationException2("لا يمكن أن يكون العنصر أبًا لنفسه.");

        if (selfId is not null && newParentId is not null)
        {
            var descendants = Descendants(all, selfId.Value);
            if (descendants.Contains(newParentId.Value))
                throw new ValidationException2("لا يمكن نقل العنصر تحت أحد فروعه.");
        }

        var dup = all.Any(t => t.Id != selfId && t.ParentId == newParentId && t.Name == name);
        if (dup)
            throw new ValidationException2("يوجد عنصر آخر بنفس الاسم تحت نفس المستوى.");
    }

    private static HashSet<int> Descendants(IReadOnlyList<TreeItem> all, int rootId)
    {
        var set = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var item in all)
            {
                if (item.ParentId == parent && set.Add(item.Id))
                    queue.Enqueue(item.Id);
            }
        }
        return set;
    }

    private static IReadOnlyList<TreeNodeDto> BuildTree(IReadOnlyList<TreeItem> flat)
    {
        var children = flat.ToLookup(x => x.ParentId);

        TreeNodeDto BuildNode(int id)
        {
            var node = flat.First(t => t.Id == id);
            var kids = children[id]
                .OrderBy(x => x.Name)
                .Select(x => BuildNode(x.Id))
                .ToList();
            return new TreeNodeDto(node.Id, node.Name, node.ParentId, false, kids);
        }

        return children[null]
            .OrderBy(x => x.Name)
            .Select(x => BuildNode(x.Id))
            .ToList();
    }

    public async Task<LookupsDto> GetLookupsAsync(CancellationToken ct = default)
    {
        var categories = await CategoryTreeAsync(ct);
        var departments = await DepartmentTreeAsync(ct);
        var locations = await LocationTreeAsync(ct);
        var suppliers = await SuppliersAsync(ct);
        var accessories = await AccessoriesAsync(ct);
        return new LookupsDto(categories, departments, locations, suppliers, accessories);
    }

    // ---------- Categories ----------
    public async Task<IReadOnlyList<TreeNodeDto>> CategoryTreeAsync(CancellationToken ct = default)
    {
        var list = (await _db.Categories.AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Select(c => new { c.Id, c.Name, c.ParentId })
                .ToListAsync(ct))
            .Select(x => new TreeItem(x.Id, x.Name, x.ParentId))
            .ToList();
        return BuildTree(list);
    }

    public async Task<TreeNodeDto> CreateCategoryAsync(TreeNodeRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var name = Clean(request.Name);

        var all = await AllCategoriesAsync(ct);
        ValidateTreeEdit(all, null, name, request.ParentId);

        var node = new Category { Name = name, ParentId = request.ParentId };
        _db.Categories.Add(node);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "Category", node.Id, null, new { node.Name, node.ParentId }, ct);
        return new TreeNodeDto(node.Id, node.Name, node.ParentId, false, Array.Empty<TreeNodeDto>());
    }

    public async Task<TreeNodeDto> UpdateCategoryAsync(int id, TreeNodeRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var node = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
                   ?? throw new NotFoundException("التصنيف غير موجود.");
        var before = new { node.Name, node.ParentId };
        var name = Clean(request.Name);

        var all = await AllCategoriesAsync(ct);
        ValidateTreeEdit(all, id, name, request.ParentId);

        node.Name = name;
        node.ParentId = request.ParentId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "Category", id, before, new { node.Name, node.ParentId }, ct);
        return new TreeNodeDto(node.Id, node.Name, node.ParentId, false, Array.Empty<TreeNodeDto>());
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var node = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
                   ?? throw new NotFoundException("التصنيف غير موجود.");
        var hasChildren = await _db.Categories.AnyAsync(c => c.ParentId == id && !c.IsDeleted, ct);
        if (hasChildren)
            throw new ValidationException2("لا يمكن حذف تصنيف له تصنيفات فرعية — احذفها أولًا.");
        var usedByAsset = await _db.Assets.AnyAsync(a => a.CategoryId == id && !a.IsDeleted, ct);
        if (usedByAsset)
            throw new ValidationException2("لا يمكن حذف التصنيف لارتباطه بأصول.");
        node.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", "Category", id, null, new { node.Name }, ct);
    }

    // ---------- Departments ----------
    public async Task<IReadOnlyList<TreeNodeDto>> DepartmentTreeAsync(CancellationToken ct = default)
    {
        var list = (await _db.Departments.AsNoTracking()
                .Where(d => !d.IsDeleted)
                .Select(d => new { d.Id, d.Name, d.ParentId })
                .ToListAsync(ct))
            .Select(x => new TreeItem(x.Id, x.Name, x.ParentId))
            .ToList();
        return BuildTree(list);
    }

    public async Task<TreeNodeDto> CreateDepartmentAsync(TreeNodeRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var name = Clean(request.Name);

        var all = await AllDepartmentsAsync(ct);
        ValidateTreeEdit(all, null, name, request.ParentId);

        var node = new Department { Name = name, ParentId = request.ParentId };
        _db.Departments.Add(node);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "Department", node.Id, null, new { node.Name, node.ParentId }, ct);
        return new TreeNodeDto(node.Id, node.Name, node.ParentId, false, Array.Empty<TreeNodeDto>());
    }

    public async Task<TreeNodeDto> UpdateDepartmentAsync(int id, TreeNodeRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var node = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
                   ?? throw new NotFoundException("الإدارة غير موجودة.");
        var beforeParent = node.ParentId;
        var before = new { node.Name, node.ParentId };
        var name = Clean(request.Name);

        var all = await AllDepartmentsAsync(ct);
        ValidateTreeEdit(all, id, name, request.ParentId);

        node.Name = name;
        if (request.ParentId != node.ParentId)
        {
            node.ParentId = request.ParentId;
            _db.OrgStructureHistory.Add(new OrgStructureHistory
            {
                DepartmentId = id,
                OldParentId = beforeParent,
                NewParentId = request.ParentId,
                ChangedBy = RequireUser(),
                ChangedAt = _clock.Now
            });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "Department", id, before, new { node.Name, node.ParentId }, ct);
        return new TreeNodeDto(node.Id, node.Name, node.ParentId, false, Array.Empty<TreeNodeDto>());
    }

    public async Task DeleteDepartmentAsync(int id, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var node = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct)
                   ?? throw new NotFoundException("الإدارة غير موجودة.");
        var hasChildren = await _db.Departments.AnyAsync(d => d.ParentId == id && !d.IsDeleted, ct);
        if (hasChildren)
            throw new ValidationException2("لا يمكن حذف إدارة لها أقسام فرعية.");
        var hasEmployees = await _db.Employees.AnyAsync(e => e.DepartmentId == id && !e.IsDeleted, ct);
        if (hasEmployees)
            throw new ValidationException2("لا يمكن حذف إدارة بها موظفون.");
        var usedByAsset = await _db.Assets.AnyAsync(a => a.DepartmentId == id && !a.IsDeleted, ct);
        if (usedByAsset)
            throw new ValidationException2("لا يمكن حذف إدارة مرتبطة بأصول.");
        node.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", "Department", id, null, new { node.Name }, ct);
    }

    // ---------- Locations ----------
    public async Task<IReadOnlyList<TreeNodeDto>> LocationTreeAsync(CancellationToken ct = default)
    {
        var list = (await _db.Locations.AsNoTracking()
                .Where(l => !l.IsDeleted)
                .Select(l => new { l.Id, l.Name, l.ParentId })
                .ToListAsync(ct))
            .Select(x => new TreeItem(x.Id, x.Name, x.ParentId))
            .ToList();
        return BuildTree(list);
    }

    public async Task<TreeNodeDto> CreateLocationAsync(TreeNodeRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var name = Clean(request.Name);

        var all = await AllLocationsAsync(ct);
        ValidateTreeEdit(all, null, name, request.ParentId);

        var node = new Location { Name = name, ParentId = request.ParentId };
        _db.Locations.Add(node);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "Location", node.Id, null, new { node.Name, node.ParentId }, ct);
        return new TreeNodeDto(node.Id, node.Name, node.ParentId, false, Array.Empty<TreeNodeDto>());
    }

    public async Task<TreeNodeDto> UpdateLocationAsync(int id, TreeNodeRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var node = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
                   ?? throw new NotFoundException("الموقع غير موجود.");
        var before = new { node.Name, node.ParentId };
        var name = Clean(request.Name);

        var all = await AllLocationsAsync(ct);
        ValidateTreeEdit(all, id, name, request.ParentId);

        node.Name = name;
        node.ParentId = request.ParentId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "Location", id, before, new { node.Name, node.ParentId }, ct);
        return new TreeNodeDto(node.Id, node.Name, node.ParentId, false, Array.Empty<TreeNodeDto>());
    }

    public async Task DeleteLocationAsync(int id, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var node = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct)
                   ?? throw new NotFoundException("الموقع غير موجود.");
        var hasChildren = await _db.Locations.AnyAsync(l => l.ParentId == id && !l.IsDeleted, ct);
        if (hasChildren)
            throw new ValidationException2("لا يمكن حذف موقع له مواقع فرعية.");
        var usedByAsset = await _db.Assets.AnyAsync(a => a.LocationId == id && !a.IsDeleted, ct);
        if (usedByAsset)
            throw new ValidationException2("لا يمكن حذف موقع مرتبط بأصول.");
        node.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", "Location", id, null, new { node.Name }, ct);
    }

    // ---------- Suppliers ----------
    public async Task<IReadOnlyList<SupplierDto>> SuppliersAsync(CancellationToken ct = default)
    {
        return await _db.Suppliers.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.ContactInfo, s.IsDeleted))
            .ToListAsync(ct);
    }

    public async Task<SupplierDto> CreateSupplierAsync(SupplierRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var supplier = new Supplier { Name = Clean(request.Name), ContactInfo = request.ContactInfo?.Trim() };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "Supplier", supplier.Id, null, new { supplier.Name }, ct);
        return new SupplierDto(supplier.Id, supplier.Name, supplier.ContactInfo, false);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(int id, SupplierRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
                       ?? throw new NotFoundException("المورد غير موجود.");
        var before = new { supplier.Name, supplier.ContactInfo };
        supplier.Name = Clean(request.Name);
        supplier.ContactInfo = request.ContactInfo?.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "Supplier", id, before, new { supplier.Name, supplier.ContactInfo }, ct);
        return new SupplierDto(supplier.Id, supplier.Name, supplier.ContactInfo, false);
    }

    public async Task DeleteSupplierAsync(int id, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
                       ?? throw new NotFoundException("المورد غير موجود.");
        var usedByAsset = await _db.Assets.AnyAsync(a => a.SupplierId == id && !a.IsDeleted, ct);
        if (usedByAsset)
            throw new ValidationException2("لا يمكن حذف مورد مرتبط بأصول.");
        supplier.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", "Supplier", id, null, new { supplier.Name }, ct);
    }

    // ---------- Accessories ----------
    public async Task<IReadOnlyList<AccessoryDto>> AccessoriesAsync(CancellationToken ct = default)
    {
        return await _db.Accessories.AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.Name)
            .Select(a => new AccessoryDto(a.Id, a.Name, a.IsLibraryItem))
            .ToListAsync(ct);
    }

    public async Task<AccessoryDto> CreateAccessoryAsync(AccessoryRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var item = new Accessory { Name = Clean(request.Name), IsLibraryItem = request.IsLibraryItem };
        _db.Accessories.Add(item);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "Accessory", item.Id, null, new { item.Name }, ct);
        return new AccessoryDto(item.Id, item.Name, item.IsLibraryItem);
    }

    public async Task<AccessoryDto> UpdateAccessoryAsync(int id, AccessoryRequest request, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var item = await _db.Accessories.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct)
                   ?? throw new NotFoundException("المشتمل غير موجود.");
        item.Name = Clean(request.Name);
        item.IsLibraryItem = request.IsLibraryItem;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "Accessory", id, null, new { item.Name }, ct);
        return new AccessoryDto(item.Id, item.Name, item.IsLibraryItem);
    }

    public async Task DeleteAccessoryAsync(int id, CancellationToken ct = default)
    {
        await RequireReferenceAdminAsync(ct);
        var item = await _db.Accessories.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct)
                   ?? throw new NotFoundException("المشتمل غير موجود.");
        var usedByAsset = await _db.AssetAccessories.AnyAsync(aa => aa.AccessoryId == id, ct);
        if (usedByAsset)
            throw new ValidationException2("لا يمكن حذف مشتمل مرتبط بأصول.");
        item.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", "Accessory", id, null, new { item.Name }, ct);
    }

    // ---------- helpers ----------
    private async Task<List<TreeItem>> AllCategoriesAsync(CancellationToken ct)
    {
        var rows = await _db.Categories.AsNoTracking().Where(c=>!c.IsDeleted).Select(c => new { c.Id, c.Name, c.ParentId }).ToListAsync(ct);
        return rows.Select(r => new TreeItem(r.Id, r.Name, r.ParentId)).ToList();
    }

    private async Task<List<TreeItem>> AllDepartmentsAsync(CancellationToken ct)
    {
        var rows = await _db.Departments.AsNoTracking().Where(d=>!d.IsDeleted).Select(d => new { d.Id, d.Name, d.ParentId }).ToListAsync(ct);
        return rows.Select(r => new TreeItem(r.Id, r.Name, r.ParentId)).ToList();
    }

    private async Task<List<TreeItem>> AllLocationsAsync(CancellationToken ct)
    {
        var rows = await _db.Locations.AsNoTracking().Where(l=>!l.IsDeleted).Select(l => new { l.Id, l.Name, l.ParentId }).ToListAsync(ct);
        return rows.Select(r => new TreeItem(r.Id, r.Name, r.ParentId)).ToList();
    }
}
