using BH_AEP.Domain.Enums;

namespace BH_AEP.Domain.Entities;

public class InventoryCampaign : BaseEntity
{
    public string Name { get; set; } = "";
    public int? DepartmentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public InventoryCampaignStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }

    public Department? Department { get; set; }
    public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}
