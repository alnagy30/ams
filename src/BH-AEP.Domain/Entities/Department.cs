namespace BH_AEP.Domain.Entities;

public class Department : BaseEntity
{
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public bool IsDeleted { get; set; }

    public Department? Parent { get; set; }
    public ICollection<Department> Children { get; set; } = new List<Department>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
