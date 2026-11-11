using BH_AEP.Application.DTOs;

namespace BH_AEP.Application.Common;

public interface IExcelWriter
{
    byte[] ToExcel(ReportResponse data);
}

public interface IPdfExportService
{
    Task<byte[]> ToPdfAsync(ReportResponse data, CancellationToken ct = default);
}

public interface IExcelImportParser
{
    Task<ParseResultDto> ParseAsync(Stream file, string fileName, CancellationToken ct = default);
}
