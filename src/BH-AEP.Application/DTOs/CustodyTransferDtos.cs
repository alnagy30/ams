using BH_AEP.Domain.Enums;

namespace BH_AEP.Application.DTOs;

public record CustodyRecordDto(
    int Id,
    int AssetId,
    string AssetCode,
    string AssetName,
    int? CustodianId,
    string? CustodianName,
    string CustodyType,
    DateTime HandoverDate,
    DateTime? ReturnDate,
    string? Notes,
    string CreatedByName,
    bool IsOpen);

public record CustodyListQuery(int? AssetId, int? EmployeeId, int? DepartmentId, bool OpenOnly, int Page = 1, int PageSize = 20);

public record TransferRecordDto(
    int Id,
    int AssetId,
    string AssetCode,
    string AssetName,
    int? FromDepartmentId,
    string? FromDepartmentName,
    int? ToDepartmentId,
    string? ToDepartmentName,
    int? FromLocationId,
    string? FromLocationName,
    int? ToLocationId,
    string? ToLocationName,
    DateTime TransferDate,
    string? Reason,
    string CreatedByName,
    bool IsBulk);

public record TransferRecordQuery(int? DepartmentId, int? AssetId, int Page = 1, int PageSize = 20);

public record SingleTransferRequest(
    int AssetId,
    int ToDepartmentId,
    int ToLocationId,
    int? RecipientEmployeeId,
    DateTime TransferDate,
    string? Reason);

public record BulkTransferRequest(
    IReadOnlyList<int> AssetIds,
    int ToDepartmentId,
    int ToLocationId,
    int? RecipientEmployeeId,
    DateTime TransferDate,
    string? Reason);

public record BulkTransferPlanDto(
    IReadOnlyList<int> IndividualAssetIds,
    IReadOnlyList<int> DepartmentGeneralAssetIds,
    IReadOnlyList<AssetListDto> Assets);

public record ReturnToStoreRequest(IReadOnlyList<int> AssetIds, DateTime Date, string? Notes);

public record BulkAssignRequest(int? DepartmentId, IReadOnlyList<int> AssetIds, int EmployeeId);
