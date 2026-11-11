namespace BH_AEP.Domain.Entities;

public class InventoryItem : BaseEntity
{
    public int CampaignId { get; set; }
    public int AssetId { get; set; }
    public bool IsFound { get; set; }
    public string? Notes { get; set; }
    public int RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; }

    public InventoryCampaign? Campaign { get; set; }
    public Asset? Asset { get; set; }
}
