using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using BH_AEP.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/assets")]
public class AssetDocumentsController : ControllerBase
{
    private readonly IAssetDocumentService _docs;

    public AssetDocumentsController(IAssetDocumentService docs)
    {
        _docs = docs;
    }

    [HttpPost("{assetId:int}/attachments")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(int assetId,
        [FromForm] AttachmentUploadRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(new { message = "الملف مطلوب." });

        var type = Enum.TryParse<AttachmentType>(request.Type, true, out var parsed)
            ? parsed
            : AttachmentType.Document;

        await using var stream = request.File.OpenReadStream();
        var result = await _docs.UploadAsync(assetId, type, request.File.FileName,
            request.File.ContentType, stream, ct);
        return Ok(result);
    }

    [HttpGet("attachments/{attachmentId:int}/download")]
    public async Task<IActionResult> Download(int attachmentId, CancellationToken ct)
    {
        var (content, mime, name) = await _docs.DownloadAsync(attachmentId, ct);
        return File(content, mime, name);
    }

    [HttpDelete("attachments/{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, CancellationToken ct)
    {
        await _docs.DeleteAsync(attachmentId, ct);
        return NoContent();
    }

    [HttpGet("{assetId:int}/card")]
    public async Task<IActionResult> AssetCard(int assetId, CancellationToken ct)
    {
        var pdf = await _docs.AssetCardAsync(assetId, ct);
        return File(pdf, "application/pdf", $"card-{assetId}.pdf");
    }
}

public record AttachmentUploadRequest(string Type, IFormFile File);
