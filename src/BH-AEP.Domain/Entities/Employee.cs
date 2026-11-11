namespace BH_AEP.Domain.Entities;

public class Employee : BaseEntity
{
    public string FullName { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string? JobTitle { get; set; }
    public int DepartmentId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public Department? Department { get; set; }
}
