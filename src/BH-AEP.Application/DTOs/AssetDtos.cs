using BH_AEP.Domain.Enums;

namespace BH_AEP.Application.DTOs;

public record VehicleDetailDto(
    int? Id,
    string? PlateNumber,
    string? ChassisNumber,
    string? EngineNumber,
    int? ManufactureYear,
    string? Color,
    string? FuelType,
    DateTime? LicenseExpiryDate,
    string? ConditionOnReceipt);

public record VehicleDetailRequest(
    string PlateNumber,
    string ChassisNumber,
    string? EngineNumber,
    int? ManufactureYear,
    string? Color,
    string? FuelType,
    DateTime? LicenseExpiryDate,
    string ConditionOnReceipt = "جديد");

public record AssetListDto(
    int Id,
    string AssetCode,
    string Name,
    int CategoryId,
    string CategoryName,
    string? SubCategoryName,
    string? Brand,
    string? Model,
    string? SerialNumber,
    string Status,
    string? CustodyType,
    string? CustodianName,
    string DepartmentName,
    string LocationName,
    decimal? PurchaseValue,
    string? PlateNumber,
    bool IsVehicle,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AssetDetailDto(
    int Id,
    string AssetCode,
    string Name,
    int CategoryId,
    string CategoryName,
    string? Brand,
    string? Model,
    string? SerialNumber,
    DateTime? PurchaseDate,
    decimal? PurchaseValue,
    int? SupplierId,
    string? SupplierName,
    int LocationId,
    string LocationName,
    int DepartmentId,
    string DepartmentName,
    int? CustodianId,
    string? CustodianName,
    CustodyType? CustodyType,
    AssetStatus Status,
    string? Notes,
    string? InspectionNumber,
    DateTime? InspectionDate,
    bool IsVehicle,
    bool HasMissingData,
    string[] MissingDataFields,
    VehicleDetailDto? Vehicle,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<AssetAccessoryDto> Accessories);

public record AttachmentDto(
    int Id,
    string AttachmentType,
    string OriginalName,
    long FileSize,
    string MimeType,
    DateTime UploadedAt);

public record AssetAccessoryDto(
    int AccessoryId,
    string AccessoryName,
    int Quantity,
    string? Notes);

public record MissingDataInfo(bool HasMissingData, string[] Fields);

public record AssetQuery(
    string? Search,
    string? AssetCode,
    int? CategoryId,
    int? DepartmentId,
    int? LocationId,
    string? Status,
    string? CustodyType,
    string? SerialNumber,
    string? PlateNumber,
    int? EmployeeId,
    bool ExcludeScrapped = true,
    bool IncludeDeleted = false,
    string? SortBy = "Id",
    bool SortDesc = true,
    int Page = 1,
    int PageSize = 20);

public record CreateAssetRequest(
    string Name,
    int CategoryId,
    string? Brand,
    string? Model,
    string? SerialNumber,
    DateTime? PurchaseDate,
    decimal? PurchaseValue,
    int? SupplierId,
    int LocationId,
    int DepartmentId,
    AssetStatus Status,
    string? Notes,
    string? InspectionNumber,
    DateTime? InspectionDate,
    bool IsVehicle,
    VehicleDetailRequest? Vehicle,
    int? CustodianEmployeeId);

public record UpdateAssetRequest(
    string Name,
    int CategoryId,
    string? Brand,
    string? Model,
    string? SerialNumber,
    DateTime? PurchaseDate,
    decimal? PurchaseValue,
    int? SupplierId,
    int LocationId,
    int DepartmentId,
    AssetStatus Status,
    string? Notes,
    string? InspectionNumber,
    DateTime? InspectionDate,
    bool IsVehicle,
    VehicleDetailRequest? Vehicle);

public record AssetBulkAssignCustodyRequest(int EmployeeId, IReadOnlyList<int> AssetIds);

public record UnassignedAssetDto(int Id, string AssetCode, string Name, string DepartmentName);
