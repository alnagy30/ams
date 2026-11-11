using BH_AEP.Application.Common;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface INotificationScanService
{
    Task<int> ScanMissingDataAsync(CancellationToken ct = default);
    Task<int> ScanVehicleLicensesAsync(CancellationToken ct = default);
    Task<int> ScanInventoryDeadlinesAsync(CancellationToken ct = default);
    Task<int> CheckAssetMissingDataAsync(int assetId, CancellationToken ct = default);
}

public class NotificationScanService : INotificationScanService
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;

    public NotificationScanService(IAppDbContext db, INotificationService notifications, IClock clock)
    {
        _db = db;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<int> ScanMissingDataAsync(CancellationToken ct = default)
    {
        var assets = await _db.Assets
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped)
            .Select(a => new { a.Id, a.AssetCode, a.SerialNumber, a.SupplierId, a.PurchaseValue })
            .ToListAsync(ct);

        var imageAssetIds = (await _db.AssetAttachments
                .Where(att => att.AttachmentType == AttachmentType.Image)
                .Select(att => att.AssetId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var custodyAssetIds = (await _db.Custodies
                .Where(c => c.ReturnDate == null)
                .Select(c => c.AssetId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var openSet = (await _db.Notifications
                .Where(n => n.Type == NotificationType.MissingData && n.Status == NotificationStatus.Open)
                .Select(n => n.RelatedEntityId)
                .ToListAsync(ct))
            .ToHashSet();

        int created = 0;

        foreach (var asset in assets)
        {
            var incomplete = new List<string>();
            if (string.IsNullOrWhiteSpace(asset.SerialNumber)) incomplete.Add("السيريال");
            if (asset.SupplierId is null) incomplete.Add("المورد");
            if (asset.PurchaseValue is null) incomplete.Add("القيمة");
            if (!imageAssetIds.Contains(asset.Id)) incomplete.Add("الصورة");
            if (!custodyAssetIds.Contains(asset.Id)) incomplete.Add("العهدة");

            if (incomplete.Count == 0 || openSet.Contains(asset.Id))
                continue;

            await _notifications.CreateAsync(
                NotificationType.MissingData, "Asset", asset.Id,
                $"الأصل {asset.AssetCode} بياناته ناقصة: {string.Join("، ", incomplete)}.", ct);
            created++;
        }

        return created;
    }

    public async Task<int> ScanVehicleLicensesAsync(CancellationToken ct = default)
    {
        var now = _clock.Now;
        var vehicles = await _db.VehicleDetails
            .Where(v => v.LicenseExpiryDate != null && !v.Asset!.IsDeleted && v.Asset.Status != AssetStatus.Scrapped)
            .Select(v => new { v.AssetId, AssetCode = v.Asset!.AssetCode, v.LicenseExpiryDate })
            .ToListAsync(ct);

        var openVehicles = await _db.Notifications
            .Where(n => n.Type == NotificationType.VehicleLicenseExpiry && n.Status == NotificationStatus.Open)
            .Select(n => n.RelatedEntityId)
            .ToListAsync(ct);
        var openSet = new HashSet<int>(openVehicles);

        int created = 0;
        foreach (var v in vehicles)
        {
            var expiry = v.LicenseExpiryDate!.Value.Date;
            var days = (expiry - now.Date).Days;

            if (days < 0 && openSet.Contains(v.AssetId))
                continue;

            string message;
            if (days < 0)
                message = $"رخصة المركبة للأصل {v.AssetCode} منتهية منذ {-days} يوم.";
            else if (days <= 7)
                message = $"رخصة المركبة للأصل {v.AssetCode} تنتهي خلال {days} يوم — إجراء عاجل.";
            else if (days <= 30)
                message = $"رخصة المركبة للأصل {v.AssetCode} تنتهي خلال {days} يوم.";
            else
                continue;

            if (openSet.Contains(v.AssetId))
                continue;

            await _notifications.CreateAsync(
                NotificationType.VehicleLicenseExpiry, "Vehicle", v.AssetId, message, ct);
            created++;
        }

        return created;
    }

    public async Task<int> ScanInventoryDeadlinesAsync(CancellationToken ct = default)
    {
        var now = _clock.Now;
        var campaigns = await _db.InventoryCampaigns
            .Where(c => c.Status == InventoryCampaignStatus.InProgress)
            .Select(c => new { c.Id, c.Name, c.EndDate })
            .ToListAsync(ct);

        var openSet = (await _db.Notifications
                .Where(n => n.Type == NotificationType.InventoryDeadline && n.Status == NotificationStatus.Open)
                .Select(n => n.RelatedEntityId)
                .ToListAsync(ct))
            .ToHashSet();

        int created = 0;
        foreach (var c in campaigns)
        {
            var days = (c.EndDate.Date - now.Date).Days;
            if (days > 1 || openSet.Contains(c.Id))
                continue;

            var message = days < 0
                ? $"حملة الجرد «{c.Name}» تجاوزت موعدها المحدد."
                : $"حملة الجرد «{c.Name}» تنتهي غدًا. يرجى مراجعتها.";

            await _notifications.CreateAsync(
                NotificationType.InventoryDeadline, "Campaign", c.Id, message, ct);
            created++;
        }

        return created;
    }

    public async Task<int> CheckAssetMissingDataAsync(int assetId, CancellationToken ct = default)
    {
        var asset = await _db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted && a.Status != AssetStatus.Scrapped, ct);
        if (asset is null)
            return 0;

        var hasImage = await _db.AssetAttachments.AnyAsync(a => a.AssetId == asset.Id && a.AttachmentType == AttachmentType.Image, ct);
        var hasOpenCustody = await _db.Custodies.AnyAsync(c => c.AssetId == asset.Id && c.ReturnDate == null, ct);
        var hasOpen = await _db.Notifications.AnyAsync(
            n => n.Type == NotificationType.MissingData && n.Status == NotificationStatus.Open
                 && n.RelatedEntityId == asset.Id, ct);
        if (hasOpen)
            return 0;

        var missing = AssetCompleteness.EvaluateMissingFields(asset, hasImage, hasOpenCustody);
        if (missing.Count == 0)
            return 0;

        await _notifications.CreateAsync(NotificationType.MissingData, "Asset", asset.Id,
            $"الأصل {asset.AssetCode} بياناته ناقصة: {string.Join("، ", missing)}.", ct);
        return 1;
    }
}
