using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.IO;

namespace RetroTracker.Services;

public class ReportService
{
    public void GenerateTrophyReport(string plataforma, List<Jogo> jogos)
    {
        try
        {
            var pastaRelatorios = Path.Combine(AppContext.BaseDirectory, "trofeus");
            Directory.CreateDirectory(pastaRelatorios);

            var nomeArquivo = plataforma.Replace("/", "-").Replace(" ", "_");
            var caminhoArquivo = Path.Combine(pastaRelatorios, $"{nomeArquivo}_expert.pdf");

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);

                    page.Header().Text($"Troféu Expert - {plataforma}").FontSize(20).Bold().FontColor(Colors.Blue.Medium);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);
                            columns.RelativeColumn();
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("#").Bold();
                            header.Cell().Text("Jogo").Bold();
                            header.Cell().Text("Horas").Bold();
                        });

                        for (int i = 0; i < jogos.Count; i++)
                        {
                            var j = jogos[i];
                            table.Cell().Text($"{i + 1}");
                            table.Cell().Text(j.Nome);
                            table.Cell().Text($"{j.Horas}h");
                        }
                    });

                    page.Footer().Text(x =>
                    {
                        x.Span("Gerado em: ");
                        x.Span(DateTime.Now.ToShortDateString());
                    });
                });
            })
            .GeneratePdf(caminhoArquivo);

            AnsiConsole.MarkupLine($"\n[green]PDF Gerado em: {caminhoArquivo}[/]");

            try
            {
                Process.Start(new ProcessStartInfo(caminhoArquivo) { UseShellExecute = true });
            }
            catch { /* Ignora */ }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Erro ao gerar PDF: {ex.Message}[/]");
        }
    }
}