namespace BH_AEP.Application.DTOs;

public record ImportedAssetRow(
    string RowNumber,
    string AssetCode,
    string Name,
    string MainCategory,
    string SubCategory,
    string? SerialNumber,
    string? Brand,
    string? Model,
    DateTime? PurchaseDate,
    decimal? PurchaseValue,
    string? SupplierName,
    string DepartmentName,
    string LocationName,
    string? CustodyEmployee,
    string? CustodyDepartmentGeneral,
    string? Status,
    bool IsVehicle,
    string? PlateNumber,
    string? ChassisNumber,
    string? EngineNumber,
    DateTime? LicenseExpiryDate,
    string? Error);

public record ParseResultDto(
    string FileName,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyList<ImportedAssetRow> Rows);

public record ImportExecuteRequest(IReadOnlyList<ImportedAssetRow> Rows, string? FileName = null);

public record ImportBatchDto(
    int Id,
    string FileName,
    int TotalRows,
    int SuccessRows,
    int FailedRows,
    DateTime ImportedAt,
    string ImportedByName);

public record ImportIssueDto(string RowNumber, string AssetCode, string Error);

public record ImportedAssetRefDto(int Id, string AssetCode, string Name);

public record ImportSummaryDto(
    int Total,
    int Success,
    int Failed,
    IReadOnlyList<ImportIssueDto> Issues,
    IReadOnlyList<ImportedAssetRefDto> CreatedAssets);
