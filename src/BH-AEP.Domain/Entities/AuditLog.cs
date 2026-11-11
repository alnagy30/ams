namespace BH_AEP.Domain.Entities;

public class AuditLog : BaseEntity
{
    public int UserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public int? EntityId { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}
