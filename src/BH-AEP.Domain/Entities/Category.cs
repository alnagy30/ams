namespace BH_AEP.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public bool IsDeleted { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
}
