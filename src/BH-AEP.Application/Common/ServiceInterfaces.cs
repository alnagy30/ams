using BH_AEP.Application.DTOs;

namespace BH_AEP.Application.Common;

public interface IQrService
{
    byte[] GeneratePng(string content);
}

public interface IAssetCardPdfService
{
    Task<byte[]> AssetCardAsync(byte[] qrPng, string assetCode, string assetName, CancellationToken ct = default);
}

public interface IBackupService
{
    Task<string> CreateBackupAsync(bool automatic, CancellationToken ct = default);
    Task<(bool Ok, string Message)> RestoreAsync(string fileName, CancellationToken ct = default);
    Task<IReadOnlyList<BackupLogDto>> ListAsync(CancellationToken ct = default);
    string BackupDirectory { get; }
}
