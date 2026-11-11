namespace BH_AEP.Domain.Entities;

public class BackupLog : BaseEntity
{
    public string FileName { get; set; } = "";
    public string? FilePath { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public bool IsAutomatic { get; set; }
}
