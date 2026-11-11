namespace BH_AEP.Domain.Entities;

public class Location : BaseEntity
{
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public bool IsDeleted { get; set; }

    public Location? Parent { get; set; }
    public ICollection<Location> Children { get; set; } = new List<Location>();
}
