using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] string? type, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new NotificationListQuery(type, status, page, pageSize);
        return Ok(await _notifications.ListAsync(query, ct));
    }

    [HttpGet("open-count")]
    public async Task<ActionResult<int>> OpenCount(CancellationToken ct)
        => Ok(await _notifications.CountOpenAsync(ct));

    [HttpPost("{id:int}/resolve")]
    public async Task<ActionResult<NotificationDto>> Resolve(int id, CancellationToken ct)
        => Ok(await _notifications.ResolveAsync(id, ct));
}
