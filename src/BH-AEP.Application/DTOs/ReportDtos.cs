namespace BH_AEP.Application.DTOs;

public record ReportFieldDto(string Key, string Label, string DataType, bool Financial);

public record ReportTableDefinitionDto(string Key, string Label, IReadOnlyList<ReportFieldDto> Fields);

public record ReportRequest(
    string Table,
    IReadOnlyList<string> Columns,
    Dictionary<string, string> Filters,
    bool IncludeScrapped = false);

public record ReportRowDto(IReadOnlyList<object?> Values);

public record ReportResponse(
    string Table,
    IReadOnlyList<ReportFieldDto> Columns,
    IReadOnlyList<ReportRowDto> Rows);

public record ReportTableListDto(IReadOnlyList<ReportTableDefinitionDto> Tables);
