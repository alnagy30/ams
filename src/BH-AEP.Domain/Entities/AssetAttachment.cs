using BH_AEP.Domain.Enums;

namespace BH_AEP.Domain.Entities;

public class AssetAttachment : BaseEntity
{
    public int AssetId { get; set; }
    public AttachmentType AttachmentType { get; set; }
    public string OriginalName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }
    public DateTime UploadedAt { get; set; }
    public int UploadedBy { get; set; }

    public Asset? Asset { get; set; }
}
