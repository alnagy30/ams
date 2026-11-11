namespace BH_AEP.Application.DTOs;

public record EmployeeDto(
    int Id,
    string FullName,
    string NationalId,
    string? JobTitle,
    int DepartmentId,
    string DepartmentName,
    bool IsDeleted,
    DateTime CreatedAt,
    int HeldAssetsCount);

public record EmployeeCreateRequest(string FullName, string NationalId, string? JobTitle, int DepartmentId);

public record EmployeeUpdateRequest(string? FullName, string? NationalId, string? JobTitle, int? DepartmentId);

public record EmployeeLeaveRequest(int? ReplacementEmployeeId, DateTime EffectiveDate);

public record EmployeeListQuery(
    string? Search,
    int? DepartmentId,
    bool IncludeDeleted,
    int Page = 1,
    int PageSize = 20);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
