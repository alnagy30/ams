namespace BH_AEP.Domain.Entities;

public class Accessory : BaseEntity
{
    public string Name { get; set; } = "";
    public bool IsLibraryItem { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
