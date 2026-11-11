namespace BH_AEP.Domain.Entities;

public class AssetAccessory : BaseEntity
{
    public int AssetId { get; set; }
    public int AccessoryId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }

    public Asset? Asset { get; set; }
    public Accessory? Accessory { get; set; }
}
