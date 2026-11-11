using BH_AEP.Domain.Enums;

namespace BH_AEP.Domain.Entities;

public class Asset : BaseEntity
{
    public string AssetCode { get; set; } = "";
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchaseValue { get; set; }
    public int? SupplierId { get; set; }
    public int LocationId { get; set; }
    public int DepartmentId { get; set; }
    public int? CustodianId { get; set; }
    public CustodyType? CustodyType { get; set; }
    public AssetStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? InspectionNumber { get; set; }
    public DateTime? InspectionDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsVehicle { get; set; }

    public Category? Category { get; set; }
    public Location? Location { get; set; }
    public Department? Department { get; set; }
    public Employee? Custodian { get; set; }
    public Supplier? Supplier { get; set; }
    public VehicleDetail? Vehicle { get; set; }
    public ICollection<Custody> Custodies { get; set; } = new List<Custody>();
    public ICollection<AssetAttachment> Attachments { get; set; } = new List<AssetAttachment>();
    public ICollection<AssetAccessory> AssetAccessories { get; set; } = new List<AssetAccessory>();
}
