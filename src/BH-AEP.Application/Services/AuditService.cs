using System.Text.Json;
using BH_AEP.Application.Common;
using BH_AEP.Domain.Entities;

namespace BH_AEP.Application.Services;

public class AuditService : IAuditService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IClock _clock;

    public AuditService(IAppDbContext db, ICurrentUser current, IClock clock)
    {
        _db = db;
        _current = current;
        _clock = clock;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false
    };

    public async Task LogAsync(string action, string entityType, int? entityId,
        object? before = null, object? after = null, CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated || _current.UserId is null)
            return;

        var entry = new AuditLog
        {
            UserId = _current.UserId.Value,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeData = before is null ? null : JsonSerializer.Serialize(before, JsonOpts),
            AfterData = after is null ? null : JsonSerializer.Serialize(after, JsonOpts),
            Timestamp = _clock.Now,
            IpAddress = _current.IpAddress
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
