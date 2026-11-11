namespace BH_AEP.Application.Common;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, int? entityId,
        object? before = null, object? after = null, CancellationToken ct = default);
}
