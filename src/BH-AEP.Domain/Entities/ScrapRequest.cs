using BH_AEP.Domain.Enums;

namespace BH_AEP.Domain.Entities;

public class ScrapRequest : BaseEntity
{
    public int AssetId { get; set; }
    public string Reason { get; set; } = "";
    public string? Decision { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public ScrapRequestStatus Status { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Asset? Asset { get; set; }
}
