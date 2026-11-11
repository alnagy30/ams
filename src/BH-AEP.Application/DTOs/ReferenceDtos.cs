namespace BH_AEP.Application.DTOs;

public record TreeNodeDto(int Id, string Name, int? ParentId, bool IsDeleted, IReadOnlyList<TreeNodeDto> Children);

public record TreeNodeRequest(string Name, int? ParentId);

public record SupplierDto(int Id, string Name, string? ContactInfo, bool IsDeleted);

public record SupplierRequest(string Name, string? ContactInfo);

public record AccessoryDto(int Id, string Name, bool IsLibraryItem);

public record AccessoryRequest(string Name, bool IsLibraryItem);

public record LookupsDto(
    IReadOnlyList<TreeNodeDto> Categories,
    IReadOnlyList<TreeNodeDto> Departments,
    IReadOnlyList<TreeNodeDto> Locations,
    IReadOnlyList<SupplierDto> Suppliers,
    IReadOnlyList<AccessoryDto> Accessories);

public record OrgChangeDto(
    int Id,
    int DepartmentId,
    string DepartmentName,
    int? OldParentId,
    string? OldParentName,
    int? NewParentId,
    string? NewParentName,
    string ChangedByName,
    DateTime ChangedAt);
