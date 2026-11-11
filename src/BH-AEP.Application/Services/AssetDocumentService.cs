using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IAssetDocumentService
{
    Task<AttachmentDto> UploadAsync(int assetId, AttachmentType type, string originalName, string mimeType, Stream file, CancellationToken ct = default);
    Task<(byte[] Content, string MimeType, string OriginalName)> DownloadAsync(int attachmentId, CancellationToken ct = default);
    Task DeleteAsync(int attachmentId, CancellationToken ct = default);
    Task<byte[]> AssetCardAsync(int assetId, CancellationToken ct = default);
}

public class AssetDocumentService : IAssetDocumentService
{
    private readonly IAppDbContext _db;
    private readonly IFileStorage _files;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;
    private readonly IAuditService _audit;
    private readonly IQrService _qr;
    private readonly IAssetCardPdfService _card;

    public AssetDocumentService(
        IAppDbContext db,
        IFileStorage files,
        ICurrentUser current,
        IClock clock,
        IAuditService audit,
        IQrService qr,
        IAssetCardPdfService card)
    {
        _db = db;
        _files = files;
        _current = current;
        _clock = clock;
        _audit = audit;
        _qr = qr;
        _card = card;
    }

    public async Task<AttachmentDto> UploadAsync(int assetId, AttachmentType type, string originalName,
        string mimeType, Stream file, CancellationToken ct = default)
    {
        if (_current.Role is not (UserRole.Admin or UserRole.AssetManager))
            throw new ForbiddenException("دورك لا يسمح برفع المرفقات.");

        var assetExists = await _db.Assets.AnyAsync(a => a.Id == assetId && !a.IsDeleted, ct);
        if (!assetExists)
            throw new NotFoundException("الأصل غير موجود.");

        var originalNameClean = Path.GetFileName(originalName);
        if (string.IsNullOrWhiteSpace(originalNameClean))
            throw new ValidationException2("اسم الملف غير صالح.");

        var ext = Path.GetExtension(originalNameClean).ToLowerInvariant();
        var stored = await _files.SaveAsync(file, ext, ct);

        var attachment = new AssetAttachment
        {
            AssetId = assetId,
            AttachmentType = type,
            OriginalName = originalNameClean,
            StoredName = stored.StoredName,
            MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType,
            FileSize = stored.Size,
            Sha256 = stored.Sha256,
            UploadedAt = _clock.Now,
            UploadedBy = _current.UserId ?? throw new UnauthorizedException()
        };

        _db.AssetAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("UploadAttachment", "AssetAttachment", attachment.Id,
            null, new { attachment.AssetId, attachment.OriginalName }, ct);

        return new AttachmentDto(attachment.Id, type.ToString(), attachment.OriginalName,
            attachment.FileSize, attachment.MimeType, attachment.UploadedAt);
    }

    public async Task<(byte[] Content, string MimeType, string OriginalName)> DownloadAsync(int attachmentId, CancellationToken ct = default)
    {
        var attachment = await _db.AssetAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new NotFoundException("المرفق غير موجود.");

        var content = await _files.ReadAsync(attachment.StoredName, ct);
        return (content, attachment.MimeType, attachment.OriginalName);
    }

    public async Task DeleteAsync(int attachmentId, CancellationToken ct = default)
    {
        if (_current.Role is not (UserRole.Admin or UserRole.AssetManager))
            throw new ForbiddenException();

        var attachment = await _db.AssetAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct)
                         ?? throw new NotFoundException("المرفق غير موجود.");

        _db.AssetAttachments.Remove(attachment);
        await _db.SaveChangesAsync(ct);
        await _files.DeleteAsync(attachment.StoredName, ct);
        await _audit.LogAsync("DeleteAttachment", "AssetAttachment", attachmentId,
            new { attachment.AssetId, attachment.OriginalName }, null, ct);
    }

    public async Task<byte[]> AssetCardAsync(int assetId, CancellationToken ct = default)
    {
        var asset = await _db.Assets.AsNoTracking()
            .Where(a => a.Id == assetId && !a.IsDeleted)
            .Select(a => new { a.AssetCode, a.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("الأصل غير موجود.");

        var qr = _qr.GeneratePng(asset.AssetCode);
        return await _card.AssetCardAsync(qr, asset.AssetCode, asset.Name, ct);
    }
}
