using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/import")]
public class ImportController : ControllerBase
{
    private readonly IImportService _import;
    private readonly IPermissionService _permissions;

    public ImportController(IImportService import, IPermissionService permissions)
    {
        _import = import;
        _permissions = permissions;
    }

    [HttpPost("parse")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<ParseResultDto>> Parse(IFormFile file, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(PermissionNames.Import, ct);

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "الملف مطلوب." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".xlsx" or ".xls"))
            return BadRequest(new { message = "صيغة الملف غير مدعومة. استخدم ملف Excel (xlsx)." });

        await using var stream = file.OpenReadStream();
        return Ok(await _import.ParseAsync(stream, file.FileName, ct));
    }

    [HttpPost("execute")]
    public async Task<ActionResult<ImportSummaryDto>> Execute(ImportExecuteRequest request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(PermissionNames.Import, ct);
        return Ok(await _import.ExecuteAsync(request.Rows, request.FileName, ct));
    }

    [HttpGet("batches")]
    public async Task<ActionResult<IReadOnlyList<ImportBatchDto>>> Batches(CancellationToken ct)
        => Ok(await _import.BatchesAsync(ct));
}
