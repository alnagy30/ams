using BH_AEP.Domain.Enums;

namespace BH_AEP.Domain.Entities;

public class Custody : BaseEntity
{
    public int AssetId { get; set; }
    public int? CustodianId { get; set; }
    public CustodyType CustodyType { get; set; }
    public DateTime HandoverDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string? Notes { get; set; }
    public int CreatedBy { get; set; }

    public Asset? Asset { get; set; }
    public Employee? Custodian { get; set; }
}
