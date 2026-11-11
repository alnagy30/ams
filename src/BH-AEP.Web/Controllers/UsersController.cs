using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken ct)
        => Ok(await _users.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
        => Ok(await _users.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest request, CancellationToken ct)
        => Ok(await _users.UpdateAsync(id, request, ct));

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(AdminResetPasswordRequest request, CancellationToken ct)
    {
        await _users.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpGet("temp-permissions")]
    public async Task<ActionResult<IReadOnlyList<TemporaryPermissionDto>>> TempPermissions(CancellationToken ct)
        => Ok(await _users.ListTempPermissionsAsync(ct));

    [HttpPost("temp-permissions")]
    public async Task<IActionResult> GrantTempPermission(GrantTemporaryPermissionRequest request, CancellationToken ct)
    {
        await _users.GrantTemporaryPermissionAsync(request, ct);
        return NoContent();
    }

    [HttpDelete("temp-permissions/{permissionId:int}")]
    public async Task<IActionResult> RevokeTempPermission(int permissionId, CancellationToken ct)
    {
        await _users.RevokeTemporaryPermissionAsync(permissionId, ct);
        return NoContent();
    }
}
