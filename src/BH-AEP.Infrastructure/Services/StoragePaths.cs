namespace BH_AEP.Infrastructure.Services;

public class StoragePaths
{
    public string Root { get; set; } = "data";
    public string DatabaseName { get; set; } = "bh-aep.db";
    public string BackupsDirectory => Path.Combine(Root, "backups");
    public string AttachmentsDirectory => Path.Combine(Root, "attachments");
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string DatabasePath => Path.Combine(Root, DatabaseName);
    public int BackupRetentionDays { get; set; } = 30;
}
