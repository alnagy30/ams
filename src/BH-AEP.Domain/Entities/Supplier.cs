namespace BH_AEP.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Name { get; set; } = "";
    public string? ContactInfo { get; set; }
    public bool IsDeleted { get; set; }
}
