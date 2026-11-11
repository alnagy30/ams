using System.Linq.Expressions;
using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationService _notifications;

    public DashboardService(IAppDbContext db, IClock clock, INotificationService notifications)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var totalAssets = await _db.Assets.CountAsync(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped, ct);
        var totalEmployees = await _db.Employees.CountAsync(e => !e.IsDeleted, ct);
        var totalDepartments = await _db.Departments.CountAsync(d => !d.IsDeleted, ct);
        var openNotifications = await _notifications.CountOpenAsync(ct);
        var inProgressCampaigns = await _db.InventoryCampaigns.CountAsync(c => c.Status == InventoryCampaignStatus.InProgress, ct);

        var now = _clock.Now;
        var expiringLicenses = await _db.VehicleDetails.CountAsync(
            v => v.LicenseExpiryDate != null && v.LicenseExpiryDate.Value.Date <= now.Date.AddDays(30) &&
                 !v.Asset!.IsDeleted && v.Asset.Status != AssetStatus.Scrapped, ct);

        var missingDataAssets = await _db.Notifications.CountAsync(
            n => n.Type == NotificationType.MissingData && n.Status == NotificationStatus.Open, ct);

        var statusGroups = await _db.Assets
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var statusLabels = new Dictionary<AssetStatus, string>
        {
            [AssetStatus.Active] = "نشط",
            [AssetStatus.UnderMaintenance] = "تحت الصيانة",
            [AssetStatus.InStorage] = "مخزن",
            [AssetStatus.Suspended] = "معلّق",
            [AssetStatus.Missing] = "مفقود"
        };

        var distribution = statusGroups
            .Select(g => new StatusCountDto(g.Status.ToString(), statusLabels.GetValueOrDefault(g.Status, g.Status.ToString()), g.Count))
            .ToList();

        var recent = await _db.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped)
            .OrderByDescending(a => a.CreatedAt)
            .Take(6)
            .Select(a => new AssetListDto(
                a.Id, a.AssetCode, a.Name, a.CategoryId,
                a.Category!.Parent != null ? a.Category.Parent.Name : a.Category.Name,
                a.Category.Parent != null ? a.Category.Name : null,
                a.Brand, a.Model, a.SerialNumber, a.Status.ToString(),
                a.CustodyType != null ? a.CustodyType.ToString() : null,
                a.Custodian != null ? a.Custodian.FullName : null,
                a.Department!.Name, a.Location!.Name, null,
                a.Vehicle != null ? a.Vehicle.PlateNumber : null, a.IsVehicle, a.CreatedAt, a.UpdatedAt))
            .ToListAsync(ct);

        var activities = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .Take(8)
            .Select(x => new AuditLogDto(
                x.Id, x.UserId,
                _db.Users.Where(u => u.Id == x.UserId).Select(u => u.Username).FirstOrDefault() ?? "",
                x.Action, x.EntityType, x.EntityId,
                x.BeforeData, x.AfterData, x.Timestamp, x.IpAddress))
            .ToListAsync(ct);

        var purchaseValues = await _db.Assets
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped)
            .Select(a => a.PurchaseValue)
            .ToListAsync(ct);
        var totalPurchaseValue = purchaseValues.Sum(v => v ?? 0m);

        var assetsByMonth = await BuildMonthlyAsync(now, ct);
        var topCategories = await BuildTopAsync(ct, a => a.CategoryId, true);
        var topDepartments = await BuildTopAsync(ct, a => a.DepartmentId, false);

        var expiringRaw = await _db.VehicleDetails.AsNoTracking()
            .Where(v => v.LicenseExpiryDate != null &&
                        v.LicenseExpiryDate.Value.Date <= now.Date.AddDays(30) &&
                        !v.Asset!.IsDeleted && v.Asset.Status != AssetStatus.Scrapped)
            .OrderBy(v => v.LicenseExpiryDate)
            .Take(10)
            .Select(v => new
            {
                v.AssetId,
                v.Asset!.AssetCode,
                v.Asset.Name,
                v.PlateNumber,
                v.LicenseExpiryDate
            })
            .ToListAsync(ct);

        var expiringList = expiringRaw
            .Where(v => v.LicenseExpiryDate.HasValue)
            .Select(v => new ExpiringLicenseDto(
                v.AssetId, v.AssetCode, v.Name, v.PlateNumber,
                v.LicenseExpiryDate!.Value,
                (int)(v.LicenseExpiryDate!.Value.Date - now.Date).TotalDays))
            .ToList();

        return new DashboardDto(
            totalAssets, totalEmployees, totalDepartments, openNotifications,
            inProgressCampaigns, expiringLicenses,
            missingDataAssets, distribution, recent, activities,
            totalPurchaseValue, assetsByMonth, topCategories, topDepartments, expiringList);
    }

    private static readonly string[] ArabicMonths =
    {
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
        "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
    };

    private async Task<IReadOnlyList<MonthCountDto>> BuildMonthlyAsync(DateTime now, CancellationToken ct)
    {
        var firstOfMonth = new DateTime(now.Year, now.Month, 1);
        var start = firstOfMonth.AddMonths(-5);

        var grouped = await _db.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped && a.CreatedAt >= start)
            .GroupBy(a => new { a.CreatedAt.Year, a.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        var map = grouped.ToDictionary(g => (g.Year, g.Month), g => g.Count);
        var result = new List<MonthCountDto>();
        for (var i = 0; i < 6; i++)
        {
            var d = start.AddMonths(i);
            map.TryGetValue((d.Year, d.Month), out var count);
            result.Add(new MonthCountDto($"{d.Year:D4}-{d.Month:D2}", $"{ArabicMonths[d.Month - 1]} {d.Year}", count));
        }
        return result;
    }

    private async Task<IReadOnlyList<NameCountDto>> BuildTopAsync(
        CancellationToken ct,
        Expression<Func<Asset, int>> keySelector,
        bool isCategory)
    {
        var groups = await _db.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status != AssetStatus.Scrapped)
            .GroupBy(keySelector)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync(ct);

        var ids = groups.Select(g => g.Id).ToList();
        if (ids.Count == 0) return Array.Empty<NameCountDto>();

        var names = isCategory
            ? await _db.Categories.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, Name = c.Parent != null ? c.Parent.Name : c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : await _db.Departments.AsNoTracking()
                .Where(d => ids.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return groups
            .Select(g => new NameCountDto(names.GetValueOrDefault(g.Id, "—"), g.Count))
            .ToList();
    }
}
