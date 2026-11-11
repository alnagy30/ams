using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using ClosedXML.Excel;

namespace BH_AEP.Infrastructure.Services;

public class ClosedXmlExcelWriter : IExcelWriter
{
    public byte[] ToExcel(ReportResponse data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(data.Table == "Employees" ? "الموظفون" : "الأصول");

        for (int c = 0; c < data.Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = data.Columns[c].Label;
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
            sheet.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            sheet.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
        }

        int r = 2;
        foreach (var row in data.Rows)
        {
            for (int c = 0; c < row.Values.Count; c++)
            {
                var value = row.Values[c];
                if (value is DateTime dt)
                    sheet.Cell(r, c + 1).Value = dt;
                else if (value is decimal d)
                    sheet.Cell(r, c + 1).Value = (double)d;
                else
                    sheet.Cell(r, c + 1).Value = value?.ToString() ?? "";
            }
            r++;
        }

        sheet.RightToLeft = true;
        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
