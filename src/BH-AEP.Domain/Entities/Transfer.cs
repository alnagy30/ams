namespace BH_AEP.Domain.Entities;

public class Transfer : BaseEntity
{
    public int AssetId { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public DateTime TransferDate { get; set; }
    public string? Reason { get; set; }
    public int CreatedBy { get; set; }
    public bool IsBulk { get; set; }

    public Asset? Asset { get; set; }
}
