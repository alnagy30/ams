using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/backup")]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backup;
    private readonly IPermissionService _permissions;

    public BackupController(IBackupService backup, IPermissionService permissions)
    {
        _backup = backup;
        _permissions = permissions;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BackupLogDto>>> List(CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(PermissionNames.Backup, ct);
        var backups = await _backup.ListAsync(ct);
        return Ok(new BackupListDto(backups, _backup.BackupDirectory, 30));
    }

    [HttpPost]
    public async Task<ActionResult<string>> Create(CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(PermissionNames.Backup, ct);
        var path = await _backup.CreateBackupAsync(false, ct);
        return Ok(new { message = "تم إنشاء النسخة الاحتياطية بنجاح.", fileName = Path.GetFileName(path) });
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore(RestoreRequest request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(PermissionNames.Backup, ct);
        var (ok, message) = await _backup.RestoreAsync(request.FileName, ct);
        return ok ? Ok(new { message }) : StatusCode(409, new { message });
    }

    [HttpGet("files/{fileName}")]
    public async Task<IActionResult> Download(string fileName, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(PermissionNames.Backup, ct);

        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName ||
            !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "اسم ملف غير صالح." });

        var fullPath = Path.Combine(_backup.BackupDirectory, fileName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "الملف غير موجود." });

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, ct);
        return File(bytes, "application/zip", fileName);
    }
}
