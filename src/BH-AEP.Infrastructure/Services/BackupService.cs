using System.IO.Compression;
using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _current;
    private readonly StoragePaths _paths;

    public BackupService(AppDbContext db, IClock clock, ICurrentUser current, StoragePaths paths)
    {
        _db = db;
        _clock = clock;
        _current = current;
        _paths = paths;
    }

    public string BackupDirectory => _paths.BackupsDirectory;

    public async Task<string> CreateBackupAsync(bool automatic, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_paths.BackupsDirectory);

        // تفريغ WAL داخل ملف القاعدة الأساسي قبل النسخ
        await _db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);

        var dbPath = _paths.DatabasePath;
        if (!File.Exists(dbPath))
            throw new AppException("ملف قاعدة البيانات غير موجود.");

        var stamp = _clock.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"bhaep-backup-{stamp}.zip";
        var zipPath = Path.Combine(_paths.BackupsDirectory, fileName);

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(dbPath, Path.GetFileName(dbPath));

            if (Directory.Exists(_paths.AttachmentsDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(_paths.AttachmentsDirectory, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(_paths.AttachmentsDirectory, file);
                    zip.CreateEntryFromFile(file, Path.Combine("attachments", relative));
                }
            }
        }

        CleanupOldBackups();

        var createdById = _current.UserId;
        _db.BackupLogs.Add(new BackupLog
        {
            FileName = fileName,
            FilePath = zipPath,
            IsSuccess = true,
            CreatedAt = _clock.Now,
            CreatedBy = createdById,
            IsAutomatic = automatic
        });
        await _db.SaveChangesAsync(ct);

        return zipPath;
    }

    public async Task<(bool Ok, string Message)> RestoreAsync(string fileName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName ||
            !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new AppException("اسم ملف استعادة غير صالح.");

        var zipPath = Path.Combine(_paths.BackupsDirectory, fileName);
        if (!File.Exists(zipPath))
            throw new AppException("ملف النسخة الاحتياطية غير موجود.");

        var staging = Path.Combine(_paths.Root, "_restore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, staging);

            var dbEntry = Path.Combine(staging, _paths.DatabaseName);
            if (!File.Exists(dbEntry))
                throw new AppException("الملف لا يحتوي على قاعدة بيانات.");

            // نقطة احتياطية قبل الاستعادة
            var pre = Path.Combine(_paths.Root, $"pre-restore-{_clock.Now:yyyyMMdd_HHmmss}.db");
            if (File.Exists(_paths.DatabasePath))
                File.Copy(_paths.DatabasePath, pre, true);

            File.Copy(dbEntry, _paths.DatabasePath, true);

            var attachmentsStage = Path.Combine(staging, "attachments");
            if (Directory.Exists(attachmentsStage))
            {
                var oldAttach = Path.Combine(_paths.Root, $"attachments-old-{_clock.Now:yyyyMMdd_HHmmss}");
                if (Directory.Exists(_paths.AttachmentsDirectory))
                    Directory.Move(_paths.AttachmentsDirectory, oldAttach);

                Directory.CreateDirectory(_paths.AttachmentsDirectory);
                foreach (var file in Directory.EnumerateFiles(attachmentsStage, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(attachmentsStage, file);
                    var dest = Path.Combine(_paths.AttachmentsDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, true);
                }

                if (Directory.Exists(oldAttach))
                    Directory.Delete(oldAttach, true);
            }

            _db.BackupLogs.Add(new BackupLog
            {
                FileName = "restore:" + fileName,
                IsSuccess = true,
                CreatedAt = _clock.Now,
                CreatedBy = _current.UserId,
                IsAutomatic = false
            });
            await _db.SaveChangesAsync(ct);

            return (true, "تمت الاستعادة بنجاح. سيتم تفعيل البيانات فورًا على الجلسات الجديدة.");
        }
        catch (IOException ex)
        {
            return (false,
                "فشلت الاستعادة لأن قاعدة البيانات قيد الاستخدام حاليًا. أوقف الخدمة مؤقتًا ثم أعد المحاولة، أو نفّذ الاستعادة من على السيرفر مباشرة. (" +
                ex.Message + ")");
        }
        catch (Exception ex)
        {
            return (false, "فشلت الاستعادة: " + ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
            }
            catch
            {
                // تجاهل
            }
        }
    }

    public async Task<IReadOnlyList<BackupLogDto>> ListAsync(CancellationToken ct = default)
    {
        return await _db.BackupLogs.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .Select(b => new BackupLogDto(
                b.Id,
                b.FileName,
                b.IsSuccess,
                b.ErrorMessage,
                b.CreatedAt,
                _db.Users.Where(u => u.Id == b.CreatedBy).Select(u => u.Username).FirstOrDefault(),
                b.IsAutomatic,
                b.FilePath != null && File.Exists(b.FilePath) ? (long?)new FileInfo(b.FilePath).Length : null))
            .ToListAsync(ct);
    }

    private void CleanupOldBackups()
    {
        var cutoff = _clock.Now.AddDays(-_paths.BackupRetentionDays);
        foreach (var file in Directory.EnumerateFiles(_paths.BackupsDirectory, "bhaep-backup-*.zip"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTime < cutoff)
            {
                try
                {
                    info.Delete();
                }
                catch
                {
                    // تجاهل
                }
            }
        }
    }
}
