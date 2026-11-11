namespace BH_AEP.Application.DTOs;

public record BackupLogDto(
    int Id,
    string FileName,
    bool IsSuccess,
    string? ErrorMessage,
    DateTime CreatedAt,
    string? CreatedByName,
    bool IsAutomatic,
    long? FileSizeBytes);

public record BackupListDto(IReadOnlyList<BackupLogDto> Backups, string BackupDirectory, int RetentionDays);

public record RestoreRequest(string FileName);

public record SettingsDto(string ServerBaseUrl, string? CompanyName);
