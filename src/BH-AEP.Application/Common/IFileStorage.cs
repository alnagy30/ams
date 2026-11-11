namespace BH_AEP.Application.Common;

public record StoredFile(string StoredName, string Sha256, long Size);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream source, string extension, CancellationToken ct = default);
    Task<byte[]> ReadAsync(string storedName, CancellationToken ct = default);
    Task DeleteAsync(string storedName, CancellationToken ct = default);
    string RootPath { get; }
}
