using BH_AEP.Application.Common;
using BH_AEP.Application.DTOs;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BH_AEP.Infrastructure.Services;

public class QuestPdfService : IPdfExportService, IAssetCardPdfService
{
    private static readonly string[] FontFamilies;

    static QuestPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableCaching = false;

        FontFamilies = OperatingSystem.IsWindows()
            ? new[] { "Arial" }
            : new[] { "DejaVu Sans", "Amiri" };

        foreach (var name in FontFamilies)
            TryRegisterFont(name, name);
    }

    private static void TryRegisterFont(string familyName, string fileName)
    {
        string[] candidates;
        if (OperatingSystem.IsWindows())
        {
            candidates = new[]
            {
                $@"C:\Windows\Fonts\{fileName}.ttf",
                $@"C:\Windows\Fonts\{fileName}.ttc"
            };
        }
        else
        {
            candidates = new[]
            {
                $"/usr/share/fonts/truetype/dejavu/{familyName}.ttf",
                $"/usr/share/fonts/dejavu/{familyName}.ttf"
            };
            if (familyName == "Amiri")
            {
                candidates = new[]
                {
                    "/usr/share/fonts/opentype/fonts-hosny-amiri/Amiri-Regular.ttf"
                };
            }
        }

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                using var stream = File.OpenRead(path);
                FontManager.RegisterFontType(familyName, stream);
                return;
            }
            catch
            {
                // تجاهل الخطوط غير القابلة للتسجيل
            }
        }
    }

    public Task<byte[]> ToPdfAsync(ReportResponse data, CancellationToken ct = default)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(FontFamilies));

                page.Header().Text(data.Table == "Employees" ? "تقرير الموظفين" : "تقرير الأصول")
                    .FontSize(16).Bold().AlignCenter();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in data.Columns)
                            columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var col in data.Columns)
                            header.Cell().Background("#1F4E78").Padding(3).Text(col.Label).FontColor(Colors.White).Bold();
                    });

                    foreach (var row in data.Rows)
                    {
                        foreach (var value in row.Values)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(3)
                                .Text(value?.ToString() ?? "");
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("الصفحة ");
                    x.CurrentPageNumber();
                    x.Span(" من ");
                    x.TotalPages();
                });
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> AssetCardAsync(byte[] qrPng, string assetCode, string assetName, CancellationToken ct = default)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6.Landscape());
                page.Margin(12);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(FontFamilies));

                page.Content().Column(col =>
                {
                    col.Item().Text("بطاقة تعريف الأصل").FontSize(13).Bold().AlignCenter();
                    col.Item().PaddingTop(6).Text($"كود الأصل: {assetCode}").Bold();
                    col.Item().PaddingTop(2).Text($"الاسم: {assetName}");
                    col.Item().PaddingTop(6).AlignCenter().Width(55f).Height(55f)
                        .Image(qrPng, ImageScaling.FitArea);
                });
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return Task.FromResult(ms.ToArray());
    }
}
