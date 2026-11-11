using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public InventoryController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryCampaignDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _inventory.ListAsync(page, pageSize, ct));

    [HttpGet("{campaignId:int}")]
    public async Task<ActionResult<CampaignProgressDto>> Get(int campaignId, CancellationToken ct)
        => Ok(await _inventory.GetCampaignAsync(campaignId, ct));

    [HttpGet("{campaignId:int}/queue")]
    public async Task<ActionResult<IReadOnlyList<AssetListDto>>> Queue(int campaignId, CancellationToken ct)
        => Ok(await _inventory.GetQueueAsync(campaignId, ct));

    [HttpPost]
    public async Task<ActionResult<InventoryCampaignDto>> Create(CreateInventoryCampaignRequest request, CancellationToken ct)
        => Ok(await _inventory.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InventoryCampaignDto>> Update(int id, UpdateInventoryCampaignRequest request, CancellationToken ct)
        => Ok(await _inventory.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<InventoryCampaignDto>> Start(int id, CancellationToken ct)
        => Ok(await _inventory.StartAsync(id, ct));

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<InventoryCampaignDto>> Complete(int id, CancellationToken ct)
        => Ok(await _inventory.CompleteAsync(id, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _inventory.CancelAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{campaignId:int}/record")]
    public async Task<IActionResult> RecordItem(int campaignId, InventoryExecuteRequest request, CancellationToken ct)
    {
        await _inventory.RecordItemAsync(campaignId, request, ct);
        return NoContent();
    }

    [HttpPost("{campaignId:int}/resolve-mismatch")]
    public async Task<IActionResult> ResolveMismatch(int campaignId, int assetId, [FromQuery] string decision, CancellationToken ct)
    {
        await _inventory.ResolveMismatchAsync(campaignId, assetId, decision, ct);
        return NoContent();
    }
}
