namespace BH_AEP.Domain.Entities;

public class OrgStructureHistory : BaseEntity
{
    public int DepartmentId { get; set; }
    public int? OldParentId { get; set; }
    public int? NewParentId { get; set; }
    public int ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}
