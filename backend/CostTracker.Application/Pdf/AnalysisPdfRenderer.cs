using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CostTracker.Application.Pdf;

public class AnalysisPdfRenderer : IPdfRenderer
{
    public byte[] Render(string referenceMonth, string analysisMarkdown)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item()
                                .Text("Análise Inteligente")
                                .FontSize(20).Bold().FontColor(Color.FromHex("#1e2937"));

                            inner.Item()
                                .Text($"Referência: {referenceMonth}")
                                .FontSize(11).FontColor(Color.FromHex("#64748b"));
                        });

                        row.ConstantItem(120).AlignRight().AlignMiddle()
                            .Text($"Gerado em {DateTime.Now:dd/MM/yyyy}")
                            .FontSize(9).FontColor(Color.FromHex("#94a3b8"));
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Color.FromHex("#e2e8f0"));
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    RenderMarkdown(col, analysisMarkdown);
                });

                page.Footer().AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Página ").FontSize(8).FontColor(Color.FromHex("#94a3b8"));
                        text.CurrentPageNumber().FontSize(8).FontColor(Color.FromHex("#94a3b8"));
                        text.Span(" de ").FontSize(8).FontColor(Color.FromHex("#94a3b8"));
                        text.TotalPages().FontSize(8).FontColor(Color.FromHex("#94a3b8"));
                    });
            });
        });

        return document.GeneratePdf();
    }

    private static void RenderMarkdown(ColumnDescriptor col, string markdown)
    {
        var lines = markdown.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("# "))
            {
                col.Item().PaddingTop(12).PaddingBottom(4)
                    .Text(line[2..])
                    .FontSize(16).Bold().FontColor(Color.FromHex("#1e2937"));
                col.Item().LineHorizontal(0.5f).LineColor(Color.FromHex("#cbd5e1"));
            }
            else if (line.StartsWith("## "))
            {
                col.Item().PaddingTop(10).PaddingBottom(2)
                    .Text(line[3..])
                    .FontSize(13).Bold().FontColor(Color.FromHex("#334155"));
            }
            else if (line.StartsWith("### "))
            {
                col.Item().PaddingTop(8).PaddingBottom(2)
                    .Text(line[4..])
                    .FontSize(11).Bold().FontColor(Color.FromHex("#475569"));
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                col.Item().PaddingLeft(12).PaddingTop(2).Row(row =>
                {
                    row.ConstantItem(12).Text("•").FontColor(Color.FromHex("#3b82f6"));
                    row.RelativeItem().Text(RenderInlineMarkdown(line[2..])).FontSize(10);
                });
            }
            else if (line.StartsWith("---"))
            {
                col.Item().PaddingTop(8).PaddingBottom(8).LineHorizontal(0.5f).LineColor(Color.FromHex("#e2e8f0"));
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                col.Item().Height(4);
            }
            else
            {
                col.Item().PaddingTop(2).Text(t =>
                {
                    RenderInlineMarkdownToText(t, line);
                });
            }
        }
    }

    private static string RenderInlineMarkdown(string text)
    {
        return text.Replace("**", "").Replace("__", "").Replace("*", "").Replace("_", "");
    }

    private static void RenderInlineMarkdownToText(TextDescriptor t, string line)
    {
        // Simple bold parsing: split on **
        var parts = line.Split("**");
        for (var i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;

            if (i % 2 == 1)
                t.Span(parts[i]).Bold();
            else
                t.Span(parts[i]);
        }
    }
}
