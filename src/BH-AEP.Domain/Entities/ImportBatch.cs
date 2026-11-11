namespace BH_AEP.Domain.Entities;

public class ImportBatch : BaseEntity
{
    public string FileName { get; set; } = "";
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public string? ErrorLog { get; set; }
    public DateTime ImportedAt { get; set; }
    public int ImportedBy { get; set; }
}
