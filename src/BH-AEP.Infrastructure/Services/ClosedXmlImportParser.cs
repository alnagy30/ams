using System.Globalization;
using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using ClosedXML.Excel;

namespace BH_AEP.Infrastructure.Services;

public class ClosedXmlImportParser : IExcelImportParser
{
    private static readonly Dictionary<string, string> HeaderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["اسم الأصل"] = "Name",
        ["الاسم"] = "Name",
        ["كود الأصل"] = "AssetCode",
        ["التصنيف الرئيسي"] = "MainCategory",
        ["الفئة الرئيسية"] = "MainCategory",
        ["التصنيف الفرعي"] = "SubCategory",
        ["الفئة الفرعية"] = "SubCategory",
        ["السيريال"] = "SerialNumber",
        ["رقم المسلسل"] = "SerialNumber",
        ["الماركة"] = "Brand",
        ["الموديل"] = "Model",
        ["تاريخ الشراء"] = "PurchaseDate",
        ["قيمة الشراء"] = "PurchaseValue",
        ["المورد"] = "SupplierName",
        ["الإدارة"] = "DepartmentName",
        ["الموقع"] = "LocationName",
        ["الحالة"] = "Status",
        ["مركبة"] = "IsVehicle",
        ["رقم اللوحة"] = "PlateNumber",
        ["رقم الشاسيه"] = "ChassisNumber",
        ["رقم الموتور"] = "EngineNumber",
        ["انتهاء الرخصة"] = "LicenseExpiryDate"
    };

    public Task<ParseResultDto> ParseAsync(Stream file, string fileName, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(file);

        var ws = workbook.Worksheets.FirstOrDefault(w => HasHeaderRow(w))
                 ?? throw new ValidationException2("لم يتم العثور على شيت بيانات صالح.");

        var headerRow = FindHeaderRow(ws);
        var mapping = BuildMapping(ws, headerRow);

        var rows = new List<ImportedAssetRow>();
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();
            var rowValues = ReadRow(ws, r, mapping);

            if (rowValues.Count == 0 || IsBlank(rowValues))
                continue;

            var rowNumber = $"صف {r}";
            var row = BuildRow(rowValues, rowNumber);

            var error = ValidateRow(row);
            if (error is not null)
                row = row with { Error = error };

            rows.Add(row);
        }

        // دمج شيت المركبات إن وُجد (يرتبط بالسيريال)
        var vehicleSheet = workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("مركبة", StringComparison.OrdinalIgnoreCase));
        if (vehicleSheet is not null)
            MergeVehicleRows(rows, vehicleSheet);

        return Task.FromResult(new ParseResultDto(
            fileName,
            rows.Count,
            rows.Count(x => x.Error is null),
            rows.Count(x => x.Error is not null),
            rows));
    }

    private static bool HasHeaderRow(IXLWorksheet ws)
    {
        for (int r = 1; r <= Math.Min(ws.LastRowUsed()?.RowNumber() ?? 0, 10); r++)
        {
            for (int c = 1; c <= Math.Min(ws.LastCellUsed()?.Address.ColumnNumber ?? 0, 20); c++)
            {
                var text = ws.Cell(r, c).GetString()?.Trim();
                if (!string.IsNullOrEmpty(text) && HeaderMap.ContainsKey(text))
                    return true;
            }
        }
        return false;
    }

    private static int FindHeaderRow(IXLWorksheet ws)
    {
        int max = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (int r = 1; r <= Math.Min(max, 10); r++)
        {
            for (int c = 1; c <= Math.Min(ws.LastCellUsed()?.Address.ColumnNumber ?? 0, 20); c++)
            {
                var text = ws.Cell(r, c).GetString()?.Trim();
                if (!string.IsNullOrEmpty(text) && HeaderMap.ContainsKey(text))
                    return r;
            }
        }
        return 1;
    }

    private static Dictionary<int, string> BuildMapping(IXLWorksheet ws, int headerRow)
    {
        var map = new Dictionary<int, string>();
        for (int c = 1; c <= (ws.LastCellUsed()?.Address.ColumnNumber ?? 0); c++)
        {
            var text = ws.Cell(headerRow, c).GetString()?.Trim();
            if (!string.IsNullOrEmpty(text) && HeaderMap.TryGetValue(text, out var key))
                map[c] = key;
        }

        if (!map.ContainsValue("Name") && !map.ContainsValue("MainCategory"))
            throw new ValidationException2("الملف لا يحتوي على الأعمدة الأساسية المطلوبة (اسم الأصل/التصنيف).");

        return map;
    }

    private static Dictionary<string, string> ReadRow(IXLWorksheet ws, int rowNumber, Dictionary<int, string> mapping)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, key) in mapping)
            result[key] = ws.Cell(rowNumber, col).GetString()?.Trim() ?? "";
        return result;
    }

    private static bool IsBlank(Dictionary<string, string> row)
        => row.Values.All(string.IsNullOrWhiteSpace);

    private static ImportedAssetRow BuildRow(Dictionary<string, string> v, string rowNumber)
    {
        var isVehicle = IsYes(v.GetValueOrDefault("IsVehicle"));

        return new ImportedAssetRow(
            rowNumber,
            v.GetValueOrDefault("AssetCode") ?? "",
            v.GetValueOrDefault("Name") ?? "",
            v.GetValueOrDefault("MainCategory") ?? "",
            v.GetValueOrDefault("SubCategory") ?? "",
            v.GetValueOrDefault("SerialNumber"),
            v.GetValueOrDefault("Brand"),
            v.GetValueOrDefault("Model"),
            ParseDate(v.GetValueOrDefault("PurchaseDate")),
            ParseDecimal(v.GetValueOrDefault("PurchaseValue")),
            v.GetValueOrDefault("SupplierName"),
            v.GetValueOrDefault("DepartmentName") ?? "",
            v.GetValueOrDefault("LocationName") ?? "",
            null,
            null,
            v.GetValueOrDefault("Status"),
            isVehicle,
            v.GetValueOrDefault("PlateNumber"),
            v.GetValueOrDefault("ChassisNumber"),
            v.GetValueOrDefault("EngineNumber"),
            ParseDate(v.GetValueOrDefault("LicenseExpiryDate")),
            null);
    }

    private static string? ValidateRow(ImportedAssetRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Name) && string.IsNullOrWhiteSpace(row.SubCategory))
            return "اسم الأصل أو التصنيف الفرعي مطلوب.";
        if (string.IsNullOrWhiteSpace(row.MainCategory))
            return "التصنيف الرئيسي مطلوب.";
        if (string.IsNullOrWhiteSpace(row.DepartmentName))
            return "الإدارة مطلوبة.";
        if (string.IsNullOrWhiteSpace(row.LocationName))
            return "الموقع مطلوب.";
        if (row.IsVehicle && (string.IsNullOrWhiteSpace(row.PlateNumber) || string.IsNullOrWhiteSpace(row.ChassisNumber)))
            return "المركبة تتطلب رقم اللوحة ورقم الشاسيه.";
        return null;
    }

    private void MergeVehicleRows(List<ImportedAssetRow> rows, IXLWorksheet ws)
    {
        // نبحث عن أعمدة السيريال/اللوحة/الشاسيه/الموتور/الرخصة في شيت المركبات
        var map = new Dictionary<string, (int SerialCol, int PlateCol, int ChassisCol, int EngineCol, int ExpiryCol)?>();
        int headerRow = FindHeaderRow(ws);
        int serialCol = 0, plateCol = 0, chassisCol = 0, engineCol = 0, expiryCol = 0;

        for (int c = 1; c <= (ws.LastCellUsed()?.Address.ColumnNumber ?? 0); c++)
        {
            var text = ws.Cell(headerRow, c).GetString()?.Trim();
            if (text is null) continue;
            if (text.Contains("سيريال") || text.Contains("مسلسل")) serialCol = c;
            else if (text.Contains("لوحة")) plateCol = c;
            else if (text.Contains("شاسيه")) chassisCol = c;
            else if (text.Contains("موتور")) engineCol = c;
            else if (text.Contains("رخصة")) expiryCol = c;
        }

        if (serialCol == 0 || (plateCol == 0 && chassisCol == 0)) return;

        var bySerial = rows.Where(r => !string.IsNullOrWhiteSpace(r.SerialNumber))
            .ToDictionary(r => r.SerialNumber!.Trim(), r => r, StringComparer.OrdinalIgnoreCase);

        for (int r = headerRow + 1; r <= (ws.LastRowUsed()?.RowNumber() ?? headerRow); r++)
        {
            var serial = ws.Cell(r, serialCol).GetString()?.Trim();
            if (string.IsNullOrEmpty(serial) || !bySerial.TryGetValue(serial, out var row))
                continue;

            var plate = plateCol > 0 ? ws.Cell(r, plateCol).GetString()?.Trim() : null;
            var chassis = chassisCol > 0 ? ws.Cell(r, chassisCol).GetString()?.Trim() : null;
            var engine = engineCol > 0 ? ws.Cell(r, engineCol).GetString()?.Trim() : null;
            var expiry = expiryCol > 0 ? ParseDate(ws.Cell(r, expiryCol).GetString()) : null;

            bySerial[serial] = row with
            {
                IsVehicle = true,
                PlateNumber = string.IsNullOrWhiteSpace(row.PlateNumber) ? plate : row.PlateNumber,
                ChassisNumber = string.IsNullOrWhiteSpace(row.ChassisNumber) ? chassis : row.ChassisNumber,
                EngineNumber = string.IsNullOrWhiteSpace(row.EngineNumber) ? engine : row.EngineNumber,
                LicenseExpiryDate = row.LicenseExpiryDate ?? expiry,
                Error = row.Error
            };
        }
    }

    private static bool IsYes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = Normalize(value);
        return v == "1" || v is "نعم" or "y" or "yes" or "true";
    }

    private static string Normalize(string value)
    {
        value = value.Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4")
            .Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9")
            .Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4")
            .Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9");
        return value.Trim();
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = Normalize(value).Replace(",", "").Replace("ج.م", "").Replace("EGP", "");
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(Normalize(value), out var i) ? i : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = Normalize(value);
        var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy", "dd/MM/yy" };
        if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;

        // Excel رقم تسلسلي للتاريخ
        if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) &&
            serial is > 20000 and < 80000)
            return new DateTime(1899, 12, 30).AddDays(serial);

        return DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
