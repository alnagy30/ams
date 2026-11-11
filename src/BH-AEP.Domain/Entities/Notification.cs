using BH_AEP.Domain.Enums;

namespace BH_AEP.Domain.Entities;

public class Notification : BaseEntity
{
    public NotificationType Type { get; set; }
    public string RelatedEntityType { get; set; } = "";
    public int RelatedEntityId { get; set; }
    public string Message { get; set; } = "";
    public NotificationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
