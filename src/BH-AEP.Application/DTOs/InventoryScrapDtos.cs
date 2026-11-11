using BH_AEP.Domain.Enums;

namespace BH_AEP.Application.DTOs;

public record InventoryCampaignDto(
    int Id,
    string Name,
    int? DepartmentId,
    string? DepartmentName,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    int AssetCount,
    int FoundCount,
    int NotFoundCount,
    DateTime CreatedAt,
    string CreatedByName);

public record CreateInventoryCampaignRequest(
    string Name,
    int? DepartmentId,
    DateTime StartDate,
    DateTime EndDate);

public record UpdateInventoryCampaignRequest(
    string? Name,
    DateTime? StartDate,
    DateTime? EndDate);

public record InventoryExecuteRequest(int AssetId, bool IsFound, string? Notes);

public record InventoryItemDto(
    int AssetId,
    string AssetCode,
    string AssetName,
    bool? IsFound,
    string? Notes,
    DateTime? RecordedAt,
    string? RecordedByName);

public record CampaignProgressDto(
    int CampaignId,
    string Name,
    IReadOnlyList<InventoryItemDto> Items,
    int AssetCount,
    int RecordedCount,
    int MissingCount);

public record ScrapRequestDto(
    int Id,
    int AssetId,
    string AssetCode,
    string AssetName,
    string Reason,
    string? Decision,
    string Status,
    DateTime CreatedAt,
    string CreatedByName,
    DateTime? ApprovedAt,
    string? ApprovedByName);

public record ScrapRequestCreate(int AssetId, string Reason);

public record ScrapRequestDecision(int Id, bool Approve, string? DecisionText);
