using BH_AEP.Application.Common;
using BH_AEP.Application.Services;
using BH_AEP.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace BH_AEP.Infrastructure.Jobs;

public class ScheduledJobs
{
    private readonly INotificationScanService _scans;
    private readonly IBackupService _backup;
    private readonly ArchivalService _archival;
    private readonly ILogger<ScheduledJobs> _logger;

    public ScheduledJobs(
        INotificationScanService scans,
        IBackupService backup,
        ArchivalService archival,
        ILogger<ScheduledJobs> logger)
    {
        _scans = scans;
        _backup = backup;
        _archival = archival;
        _logger = logger;
    }

    public async Task DailyScansAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Scheduled daily scan started.");
        var missing = await _scans.ScanMissingDataAsync(ct);
        var licenses = await _scans.ScanVehicleLicensesAsync(ct);
        var inventory = await _scans.ScanInventoryDeadlinesAsync(ct);
        _logger.LogInformation(
            "Daily scan done: missing={Missing}, licenses={Licenses}, inventory={Inventory}.",
            missing, licenses, inventory);
    }

    public async Task WeeklyBackupAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Scheduled weekly backup started.");
        var path = await _backup.CreateBackupAsync(automatic: true, ct);
        _logger.LogInformation("Scheduled weekly backup created at {Path}.", path);
    }

    public async Task MonthlyArchivalAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Scheduled monthly archival started.");
        var (audit, notifications) = await _archival.CleanupAsync(ct: ct);
        _logger.LogInformation("Scheduled monthly archival done: {Audit} audit, {Notifications} notifications.",
            audit, notifications);
    }
}
