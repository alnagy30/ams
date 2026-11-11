using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditQueryService _audit;

    public AuditController(IAuditQueryService audit)
    {
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        [FromQuery] string? action, [FromQuery] string? entityType, [FromQuery] string? userName,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new AuditLogQuery(action, entityType, userName, from, to, page, pageSize);
        return Ok(await _audit.ListAsync(query, ct));
    }

    [HttpGet("entity")]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> ForEntity(
        [FromQuery] string entityType, [FromQuery] int entityId, CancellationToken ct)
        => Ok(await _audit.ForEntityAsync(entityType, entityId, ct));

    [HttpGet("org-history")]
    public async Task<ActionResult<PagedResult<OrgChangeDto>>> OrgHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _audit.OrgHistoryAsync(page, pageSize, ct));
}
