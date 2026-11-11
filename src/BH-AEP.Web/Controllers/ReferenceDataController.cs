using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/reference")]
public class ReferenceDataController : ControllerBase
{
    private readonly IReferenceDataService _reference;

    public ReferenceDataController(IReferenceDataService reference)
    {
        _reference = reference;
    }

    [HttpGet("lookups")]
    public async Task<ActionResult<LookupsDto>> Lookups(CancellationToken ct)
        => Ok(await _reference.GetLookupsAsync(ct));

    // ---------- Categories ----------
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<TreeNodeDto>>> Categories(CancellationToken ct)
        => Ok(await _reference.CategoryTreeAsync(ct));

    [HttpPost("categories")]
    public async Task<ActionResult<TreeNodeDto>> CreateCategory(TreeNodeRequest request, CancellationToken ct)
        => Ok(await _reference.CreateCategoryAsync(request, ct));

    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<TreeNodeDto>> UpdateCategory(int id, TreeNodeRequest request, CancellationToken ct)
        => Ok(await _reference.UpdateCategoryAsync(id, request, ct));

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        await _reference.DeleteCategoryAsync(id, ct);
        return NoContent();
    }

    // ---------- Departments ----------
    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<TreeNodeDto>>> Departments(CancellationToken ct)
        => Ok(await _reference.DepartmentTreeAsync(ct));

    [HttpPost("departments")]
    public async Task<ActionResult<TreeNodeDto>> CreateDepartment(TreeNodeRequest request, CancellationToken ct)
        => Ok(await _reference.CreateDepartmentAsync(request, ct));

    [HttpPut("departments/{id:int}")]
    public async Task<ActionResult<TreeNodeDto>> UpdateDepartment(int id, TreeNodeRequest request, CancellationToken ct)
        => Ok(await _reference.UpdateDepartmentAsync(id, request, ct));

    [HttpDelete("departments/{id:int}")]
    public async Task<IActionResult> DeleteDepartment(int id, CancellationToken ct)
    {
        await _reference.DeleteDepartmentAsync(id, ct);
        return NoContent();
    }

    // ---------- Locations ----------
    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyList<TreeNodeDto>>> Locations(CancellationToken ct)
        => Ok(await _reference.LocationTreeAsync(ct));

    [HttpPost("locations")]
    public async Task<ActionResult<TreeNodeDto>> CreateLocation(TreeNodeRequest request, CancellationToken ct)
        => Ok(await _reference.CreateLocationAsync(request, ct));

    [HttpPut("locations/{id:int}")]
    public async Task<ActionResult<TreeNodeDto>> UpdateLocation(int id, TreeNodeRequest request, CancellationToken ct)
        => Ok(await _reference.UpdateLocationAsync(id, request, ct));

    [HttpDelete("locations/{id:int}")]
    public async Task<IActionResult> DeleteLocation(int id, CancellationToken ct)
    {
        await _reference.DeleteLocationAsync(id, ct);
        return NoContent();
    }

    // ---------- Suppliers ----------
    [HttpGet("suppliers")]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> Suppliers(CancellationToken ct)
        => Ok(await _reference.SuppliersAsync(ct));

    [HttpPost("suppliers")]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(SupplierRequest request, CancellationToken ct)
        => Ok(await _reference.CreateSupplierAsync(request, ct));

    [HttpPut("suppliers/{id:int}")]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(int id, SupplierRequest request, CancellationToken ct)
        => Ok(await _reference.UpdateSupplierAsync(id, request, ct));

    [HttpDelete("suppliers/{id:int}")]
    public async Task<IActionResult> DeleteSupplier(int id, CancellationToken ct)
    {
        await _reference.DeleteSupplierAsync(id, ct);
        return NoContent();
    }

    // ---------- Accessories ----------
    [HttpGet("accessories")]
    public async Task<ActionResult<IReadOnlyList<AccessoryDto>>> Accessories(CancellationToken ct)
        => Ok(await _reference.AccessoriesAsync(ct));

    [HttpPost("accessories")]
    public async Task<ActionResult<AccessoryDto>> CreateAccessory(AccessoryRequest request, CancellationToken ct)
        => Ok(await _reference.CreateAccessoryAsync(request, ct));

    [HttpPut("accessories/{id:int}")]
    public async Task<ActionResult<AccessoryDto>> UpdateAccessory(int id, AccessoryRequest request, CancellationToken ct)
        => Ok(await _reference.UpdateAccessoryAsync(id, request, ct));

    [HttpDelete("accessories/{id:int}")]
    public async Task<IActionResult> DeleteAccessory(int id, CancellationToken ct)
    {
        await _reference.DeleteAccessoryAsync(id, ct);
        return NoContent();
    }
}
