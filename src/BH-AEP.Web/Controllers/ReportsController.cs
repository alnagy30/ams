using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using BH_AEP.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BH_AEP.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly IExcelWriter _excel;
    private readonly IPdfExportService _pdf;

    public ReportsController(IReportService reports, IExcelWriter excel, IPdfExportService pdf)
    {
        _reports = reports;
        _excel = excel;
        _pdf = pdf;
    }

    [HttpGet("tables")]
    public async Task<ActionResult<ReportTableListDto>> Tables(CancellationToken ct)
        => Ok(await _reports.TablesAsync(ct));

    [HttpPost("run")]
    public async Task<ActionResult<ReportResponse>> Run(ReportRequest request, CancellationToken ct)
        => Ok(await _reports.RunAsync(request, ct));

    [HttpPost("export")]
    public async Task<IActionResult> Export(ReportRequest request, [FromQuery] string format = "excel",
        CancellationToken ct = default)
    {
        var data = await _reports.RunAsync(request, ct);
        return format.ToLowerInvariant() switch
        {
            "pdf" => File(await _pdf.ToPdfAsync(data, ct), "application/pdf",
                $"report-{request.Table}-{DateTime.Now:yyyyMMddHHmm}.pdf"),
            _ => File(_excel.ToExcel(data),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"report-{request.Table}-{DateTime.Now:yyyyMMddHHmm}.xlsx")
        };
    }
}
