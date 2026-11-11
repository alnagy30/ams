using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/scrap")]
public class ScrapController : ControllerBase
{
    private readonly IScrapService _scrap;

    public ScrapController(IScrapService scrap)
    {
        _scrap = scrap;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ScrapRequestDto>>> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _scrap.ListAsync(status, page, pageSize, ct));

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ScrapRequestDto>>> History(CancellationToken ct)
        => Ok(await _scrap.ScrappedAssetsHistoryAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ScrapRequestDto>> Create(ScrapRequestCreate request, CancellationToken ct)
        => Ok(await _scrap.CreateAsync(request, ct));

    [HttpPost("decide")]
    public async Task<ActionResult<ScrapRequestDto>> Decide(ScrapRequestDecision decision, CancellationToken ct)
        => Ok(await _scrap.DecideAsync(decision, ct));
}
