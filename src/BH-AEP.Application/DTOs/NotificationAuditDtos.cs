using BH_AEP.Domain.Enums;

namespace BH_AEP.Application.DTOs;

public record NotificationDto(
    int Id,
    string Type,
    string RelatedEntityType,
    int RelatedEntityId,
    string Message,
    string Status,
    DateTime CreatedAt,
    int? ResolvedBy,
    string? ResolvedByName,
    DateTime? ResolvedAt);

public record NotificationListQuery(string? Type, string? Status, int Page = 1, int PageSize = 20);

public record ResolveNotificationRequest(int Id);

public record AuditLogDto(
    int Id,
    int UserId,
    string UserName,
    string Action,
    string EntityType,
    int? EntityId,
    string? BeforeData,
    string? AfterData,
    DateTime Timestamp,
    string? IpAddress);

public record AuditLogQuery(string? Action, string? EntityType, string? UserName, DateTime? From, DateTime? To, int Page = 1, int PageSize = 20);
