namespace BH_AEP.Domain.Entities;

public class VehicleDetail : BaseEntity
{
    public int AssetId { get; set; }
    public string PlateNumber { get; set; } = "";
    public string ChassisNumber { get; set; } = "";
    public string? EngineNumber { get; set; }
    public int? ManufactureYear { get; set; }
    public string? Color { get; set; }
    public string? FuelType { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    public string ConditionOnReceipt { get; set; } = "جديد";

    public Asset? Asset { get; set; }
}
