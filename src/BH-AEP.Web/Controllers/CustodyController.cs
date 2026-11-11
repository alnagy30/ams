using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using BH_AEP.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/assets")]
public class CustodyController : ControllerBase
{
    private readonly ICustodyService _custody;

    public CustodyController(ICustodyService custody)
    {
        _custody = custody;
    }

    [HttpPost("{assetId:int}/custody/assign")]
    public async Task<ActionResult<CustodyRecordDto>> Assign(int assetId,
        [FromQuery] int? employeeId, [FromQuery] CustodyType type = CustodyType.Individual,
        [FromQuery] DateTime? date = null, [FromQuery] string? notes = null, CancellationToken ct = default)
    {
        var result = await _custody.AssignAsync(assetId, employeeId, type,
            date ?? DateTime.Today, notes, ct);
        return Ok(result);
    }

    [HttpPost("custody/assign-many")]
    public async Task<ActionResult<int>> AssignMany(AssetBulkAssignCustodyRequest request, CancellationToken ct)
        => Ok(await _custody.AssignManyAsync(request.EmployeeId, request.AssetIds, DateTime.Today, ct));

    [HttpGet("unassigned")]
    public async Task<ActionResult<IReadOnlyList<UnassignedAssetDto>>> Unassigned(
        [FromQuery] int? departmentId, CancellationToken ct)
        => Ok(await _custody.ListUnassignedAsync(departmentId, ct));

    [HttpGet("custody")]
    public async Task<ActionResult<PagedResult<CustodyRecordDto>>> List(
        [FromQuery] int? assetId, [FromQuery] int? employeeId, [FromQuery] int? departmentId,
        [FromQuery] bool openOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new CustodyListQuery(assetId, employeeId, departmentId, openOnly, page, pageSize);
        return Ok(await _custody.ListAsync(query, ct));
    }

    [HttpPost("transfers/single")]
    public async Task<ActionResult<TransferRecordDto>> TransferSingle(SingleTransferRequest request, CancellationToken ct)
        => Ok(await _custody.TransferSingleAsync(request, ct));

    [HttpPost("transfers/bulk/plan")]
    public async Task<ActionResult<BulkTransferPlanDto>> PlanBulk(IReadOnlyList<int> assetIds, CancellationToken ct)
        => Ok(await _custody.PlanBulkAsync(assetIds, ct));

    [HttpPost("transfers/bulk")]
    public async Task<ActionResult<int>> BulkTransfer(BulkTransferRequest request, CancellationToken ct)
        => Ok(await _custody.BulkTransferAsync(request, ct));

    [HttpPost("custody/return")]
    public async Task<ActionResult<int>> ReturnToStore(ReturnToStoreRequest request, CancellationToken ct)
        => Ok(await _custody.ReturnToStoreAsync(request, ct));

    [HttpGet("transfers/history")]
    public async Task<ActionResult<PagedResult<TransferRecordDto>>> History(
        [FromQuery] int? departmentId, [FromQuery] int? assetId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new TransferRecordQuery(departmentId, assetId, page, pageSize);
        return Ok(await _custody.HistoryAsync(query, ct));
    }

    [HttpPost("{assetId:int}/custody/transfer-to-employee")]
    public async Task<IActionResult> TransferToEmployee(int assetId,
        [FromQuery] int employeeId, [FromQuery] DateTime? date = null, [FromQuery] string? notes = null,
        CancellationToken ct = default)
    {
        await _custody.TransferToEmployeeAsync(assetId, employeeId, date ?? DateTime.Today, notes, ct);
        return NoContent();
    }
}
