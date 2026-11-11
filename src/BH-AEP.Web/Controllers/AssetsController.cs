using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/assets")]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assets;

    public AssetsController(IAssetService assets)
    {
        _assets = assets;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetListDto>>> List(
        [FromQuery] string? search,
        [FromQuery] string? assetCode,
        [FromQuery] int? categoryId,
        [FromQuery] int? departmentId,
        [FromQuery] int? locationId,
        [FromQuery] string? status,
        [FromQuery] string? custodyType,
        [FromQuery] string? serialNumber,
        [FromQuery] string? plateNumber,
        [FromQuery] int? employeeId,
        [FromQuery] bool excludeScrapped = true,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] string? sortBy = "Id",
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new AssetQuery(search, assetCode, categoryId, departmentId, locationId, status,
            custodyType, serialNumber, plateNumber, employeeId, excludeScrapped, includeDeleted,
            sortBy, sortDesc, page, pageSize);
        return Ok(await _assets.ListAsync(query, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssetDetailDto>> Get(int id, CancellationToken ct)
        => Ok(await _assets.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<AssetDetailDto>> Create(CreateAssetRequest request, CancellationToken ct)
        => Ok(await _assets.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AssetDetailDto>> Update(int id, UpdateAssetRequest request, CancellationToken ct)
        => Ok(await _assets.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> SoftDelete(int id, CancellationToken ct)
    {
        await _assets.SoftDeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/accessories")]
    public async Task<IActionResult> SetAccessories(int id, IReadOnlyList<AssetAccessoryDto> items, CancellationToken ct)
    {
        await _assets.SetAccessoriesAsync(id, items, ct);
        return NoContent();
    }
}
