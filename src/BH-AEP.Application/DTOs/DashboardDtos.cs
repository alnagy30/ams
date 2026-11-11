namespace BH_AEP.Application.DTOs;

public record StatusCountDto(string Status, string Label, int Count);

public record MonthCountDto(string Month, string Label, int Count);

public record NameCountDto(string Name, int Count);

public record ExpiringLicenseDto(
    int AssetId,
    string AssetCode,
    string Name,
    string? PlateNumber,
    DateTime LicenseExpiryDate,
    int DaysRemaining);

public record DashboardDto(
    int TotalActiveAssets,
    int TotalEmployees,
    int TotalDepartments,
    int OpenNotifications,
    int InProgressCampaigns,
    int ExpiringVehicleLicenses,
    int MissingDataAssets,
    IReadOnlyList<StatusCountDto> StatusDistribution,
    IReadOnlyList<AssetListDto> RecentlyAdded,
    IReadOnlyList<AuditLogDto> RecentActivities,
    decimal TotalPurchaseValue,
    IReadOnlyList<MonthCountDto> AssetsByMonth,
    IReadOnlyList<NameCountDto> TopCategories,
    IReadOnlyList<NameCountDto> TopDepartments,
    IReadOnlyList<ExpiringLicenseDto> ExpiringLicenses);
