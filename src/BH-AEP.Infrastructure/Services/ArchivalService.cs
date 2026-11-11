using BH_AEP.Application.Common;
using BH_AEP.Domain.Enums;
using BH_AEP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BH_AEP.Infrastructure.Services;

public class ArchivalService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<ArchivalService> _logger;

    public ArchivalService(AppDbContext db, IClock clock, ILogger<ArchivalService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>حذف سجلات التدقيق والتنبيهات المحلولة الأقدم من المدد المحددة.</summary>
    public async Task<(int AuditLogs, int Notifications)> CleanupAsync(
        int auditLogDays = 730,
        int resolvedNotificationDays = 180,
        CancellationToken ct = default)
    {
        var auditCutoff = _clock.Now.AddDays(-auditLogDays);
        var notifCutoff = _clock.Now.AddDays(-resolvedNotificationDays);

        var auditLogs = await _db.AuditLogs
            .Where(l => l.Timestamp < auditCutoff)
            .CountAsync(ct);

        if (auditLogs > 0)
        {
            await _db.AuditLogs
                .Where(l => l.Timestamp < auditCutoff)
                .ExecuteDeleteAsync(ct);
        }

        var notifications = await _db.Notifications
            .Where(n => n.Status == NotificationStatus.Resolved && n.ResolvedAt < notifCutoff)
            .CountAsync(ct);

        if (notifications > 0)
        {
            await _db.Notifications
                .Where(n => n.Status == NotificationStatus.Resolved && n.ResolvedAt < notifCutoff)
                .ExecuteDeleteAsync(ct);
        }

        _logger.LogInformation("Archival cleanup: {AuditLogs} audit logs, {Notifications} notifications removed.",
            auditLogs, notifications);
        return (auditLogs, notifications);
    }
}
