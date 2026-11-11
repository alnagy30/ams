using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employees;

    public EmployeesController(IEmployeeService employees)
    {
        _employees = employees;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> List(
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new EmployeeListQuery(search, departmentId, includeDeleted, page, pageSize);
        return Ok(await _employees.ListAsync(query, ct));
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(EmployeeCreateRequest request, CancellationToken ct)
        => Ok(await _employees.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, EmployeeUpdateRequest request, CancellationToken ct)
        => Ok(await _employees.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave(int id, EmployeeLeaveRequest request, CancellationToken ct)
    {
        await _employees.LeaveAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken ct)
    {
        await _employees.ReactivateAsync(id, ct);
        return NoContent();
    }
}
