using System.Security.Cryptography;
using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;

namespace BH_AEP.Infrastructure.Services;

public class FileStorageOptions
{
    public string RootPath { get; set; } = "attachments";
}

public class FileStorage : IFileStorage
{
    private readonly string _root;

    public FileStorage(FileStorageOptions options)
    {
        _root = Path.GetFullPath(options.RootPath);
        Directory.CreateDirectory(_root);
    }

    public string RootPath => _root;

    public async Task<StoredFile> SaveAsync(Stream source, string extension, CancellationToken ct = default)
    {
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_root, storedName);

        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            sha.AppendData(buffer, 0, read);
        }

        await output.FlushAsync(ct);
        var hash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        var size = new FileInfo(fullPath).Length;

        return new StoredFile(storedName, hash, size);
    }

    public Task<byte[]> ReadAsync(string storedName, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_root, SafeName(storedName));
        if (!File.Exists(fullPath))
            throw new NotFoundException("الملف غير موجود.");
        return File.ReadAllBytesAsync(fullPath, ct);
    }

    public Task DeleteAsync(string storedName, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_root, SafeName(storedName));
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private static string SafeName(string storedName)
    {
        var name = Path.GetFileName(storedName);
        if (string.IsNullOrWhiteSpace(name) || name != storedName)
            throw new NotFoundException("اسم ملف غير صالح.");
        return name;
    }
}
