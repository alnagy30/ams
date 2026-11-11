using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery query, CancellationToken ct = default);
    Task<EmployeeDto> CreateAsync(EmployeeCreateRequest request, CancellationToken ct = default);
    Task<EmployeeDto> UpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken ct = default);
    Task LeaveAsync(int id, EmployeeLeaveRequest request, CancellationToken ct = default);
    Task ReactivateAsync(int id, CancellationToken ct = default);
}

public class EmployeeService : IEmployeeService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly IPermissionService _permissions;
    private readonly ICustodyService _custody;

    public EmployeeService(
        IAppDbContext db,
        ICurrentUser current,
        IClock clock,
        IAuditService audit,
        IPermissionService permissions,
        ICustodyService custody)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _audit = audit;
        _permissions = permissions;
        _custody = custody;
    }

    private async Task RequireEmployeeAdminAsync(CancellationToken ct)
    {
        if (_current.Role != UserRole.Admin &&
            !await _permissions.HasPermissionAsync(PermissionNames.ManageEmployees, ct))
            throw new ForbiddenException("إدارة الموظفين متاحة للادمن أو عبر تفويض مؤقت.");
    }

    public async Task<PagedResult<EmployeeDto>> ListAsync(EmployeeListQuery query, CancellationToken ct = default)
    {
        var q = _db.Employees.AsNoTracking().AsQueryable();

        if (!query.IncludeDeleted)
            q = q.Where(e => !e.IsDeleted);

        if (query.DepartmentId.HasValue)
            q = q.Where(e => e.DepartmentId == query.DepartmentId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(e => e.FullName.Contains(search) || e.NationalId.Contains(search));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new EmployeeDto(
                e.Id,
                e.FullName,
                e.NationalId,
                e.JobTitle,
                e.DepartmentId,
                e.Department!.Name,
                e.IsDeleted,
                e.CreatedAt,
                _db.Custodies.Count(c => c.CustodianId == e.Id && c.ReturnDate == null)))
            .ToListAsync(ct);

        return new PagedResult<EmployeeDto>(items, total, query.Page, query.PageSize);
    }

    public async Task<EmployeeDto> CreateAsync(EmployeeCreateRequest request, CancellationToken ct = default)
    {
        await RequireEmployeeAdminAsync(ct);

        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ValidationException2("اسم الموظف مطلوب.");

        if (string.IsNullOrWhiteSpace(request.NationalId))
            throw new ValidationException2("الرقم القومي مطلوب.");

        var deptExists = await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId && !d.IsDeleted, ct);
        if (!deptExists)
            throw new ValidationException2("الإدارة غير موجودة.");

        var natIdExists = await _db.Employees.AnyAsync(e => e.NationalId == request.NationalId.Trim() && !e.IsDeleted, ct);
        if (natIdExists)
            throw new ValidationException2("رقم قومي مكرر — يوجد موظف مسجل بهذا الرقم.");

        var employee = new Employee
        {
            FullName = request.FullName.Trim(),
            NationalId = request.NationalId.Trim(),
            JobTitle = request.JobTitle?.Trim(),
            DepartmentId = request.DepartmentId,
            CreatedAt = _clock.Now
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "Employee", employee.Id, null,
            new { employee.FullName, employee.NationalId, employee.DepartmentId }, ct);

        return await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken ct = default)
    {
        await RequireEmployeeAdminAsync(ct);

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
                       ?? throw new NotFoundException("الموظف غير موجود.");

        var before = new { employee.FullName, employee.NationalId, employee.JobTitle, employee.DepartmentId };

        if (!string.IsNullOrWhiteSpace(request.FullName))
            employee.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.NationalId))
        {
            var dup = await _db.Employees.AnyAsync(e => e.NationalId == request.NationalId.Trim() && e.Id != id && !e.IsDeleted, ct);
            if (dup)
                throw new ValidationException2("رقم قومي مكرر — يوجد موظف مسجل بهذا الرقم.");
            employee.NationalId = request.NationalId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.JobTitle))
            employee.JobTitle = request.JobTitle;

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId.Value && !d.IsDeleted, ct);
            if (!deptExists)
                throw new ValidationException2("الإدارة غير موجودة.");
            employee.DepartmentId = request.DepartmentId.Value;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "Employee", employee.Id, before,
            new { employee.FullName, employee.NationalId, employee.JobTitle, employee.DepartmentId }, ct);

        return await ToDto(employee, ct);
    }

    public async Task LeaveAsync(int id, EmployeeLeaveRequest request, CancellationToken ct = default)
    {
        await RequireEmployeeAdminAsync(ct);

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct)
                       ?? throw new NotFoundException("الموظف غير موجود.");

        var openCustodies = await _db.Custodies
            .Where(c => c.CustodianId == id && c.ReturnDate == null)
            .Select(c => c.AssetId)
            .ToListAsync(ct);

        if (openCustodies.Count > 0 && request.ReplacementEmployeeId is null)
            throw new ValidationException2(
                $"الموظف لديه {openCustodies.Count} أصل تحت عهدته — يجب اختيار موظف بديل لنقل العهدة.");

        if (request.ReplacementEmployeeId.HasValue)
        {
            var replacement = await _db.Employees
                .FirstOrDefaultAsync(e => e.Id == request.ReplacementEmployeeId.Value && !e.IsDeleted, ct)
                ?? throw new ValidationException2("الموظف البديل غير موجود.");

            foreach (var assetId in openCustodies)
            {
                await _custody.TransferToEmployeeAsync(assetId, replacement.Id, request.EffectiveDate, "نقل تلقائي بعد ترك الموظف للشركة", ct);
            }
        }

        employee.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Leave", "Employee", employee.Id, null,
            new { employee.FullName, ReplacementEmployeeId = request.ReplacementEmployeeId, TransferredAssets = openCustodies.Count }, ct);
    }

    public async Task ReactivateAsync(int id, CancellationToken ct = default)
    {
        await RequireEmployeeAdminAsync(ct);

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, ct)
                       ?? throw new NotFoundException("الموظف غير موجود.");

        employee.IsDeleted = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Reactivate", "Employee", employee.Id, null, new { employee.FullName }, ct);
    }

    private async Task<EmployeeDto> ToDto(Employee e, CancellationToken ct)
    {
        var dept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == e.DepartmentId, ct);
        var count = await _db.Custodies.CountAsync(c => c.CustodianId == e.Id && c.ReturnDate == null, ct);

        return new EmployeeDto(
            e.Id, e.FullName, e.NationalId, e.JobTitle, e.DepartmentId,
            dept?.Name ?? "", e.IsDeleted, e.CreatedAt, count);
    }
}
