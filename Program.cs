using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.IO;

namespace RetroTracker;

class Program
{
    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "retrotracker.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";

    private static readonly string[] Plataformas = new[]
    {
        "NES/Famicom", "Super Nintendo (SNES)", "Mega Drive/Genesis", "PS1", 
        "PS2", "Nintendo 64", "Nintendo DS", "PSP", "GameCube"
    };

    static void Main(string[] args)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        InicializarBancoDeDados();

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("RetroTracker").Centered().Color(Spectre.Console.Color.Purple));

            var opcao = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Menu Principal[/]")
                    .AddChoices(new[] { "Novo Zeramento", "Ver Catálogo", "Status dos Troféus", "Sair" }));

            switch (opcao)
            {
                case "Novo Zeramento":
                    AdicionarJogo();
                    break;
                case "Ver Catálogo":
                    VerCatalogo();
                    break;
                case "Status dos Troféus":
                    VerStatusTropheu();
                    break;
                case "Sair":
                    return;
            }
        }
    }

    static void InicializarBancoDeDados()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        
        var tableSql = @"
            CREATE TABLE IF NOT EXISTS Jogos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nome TEXT NOT NULL,
                Plataforma TEXT NOT NULL,
                Horas INTEGER NOT NULL,
                DataConclusao TEXT NOT NULL
            );";
            
        using var command = connection.CreateCommand();
        command.CommandText = tableSql;
        command.ExecuteNonQuery();
    }

    static void AdicionarJogo()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]--- Adicionar Novo Jogo ---[/]\n");

        var plataforma = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Qual a [green]Plataforma[/]?")
                .PageSize(10)
                .AddChoices(Plataformas));

        var nome = AnsiConsole.Ask<string>("[bold]Nome do Jogo:[/] ");
        var horas = AnsiConsole.Ask<int>("[bold]Quantas horas?[/] ");
        var data = DateTime.Now.ToString("yyyy-MM-dd");

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        
        // Comando SQL puro (compatível com AOT)
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Jogos (Nome, Plataforma, Horas, DataConclusao) VALUES ($nome, $plataforma, $horas, $data)";
        command.Parameters.AddWithValue("$nome", nome);
        command.Parameters.AddWithValue("$plataforma", plataforma);
        command.Parameters.AddWithValue("$horas", horas);
        command.Parameters.AddWithValue("$data", data);
        
        command.ExecuteNonQuery();

        AnsiConsole.MarkupLine($"\n[bold green]✓ Salvo com sucesso![/]");

        VerificarEProgresso(plataforma, connection);

        AnsiConsole.MarkupLine("\n[dim]Pressione qualquer tecla para voltar ao menu...[/]");
        Console.ReadKey();
    }

    static void VerificarEProgresso(string plataforma, SqliteConnection connection)
    {
        int total = 0;
        
        // Contagem pura
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Jogos WHERE Plataforma = $p";
            cmd.Parameters.AddWithValue("$p", plataforma);
            total = Convert.ToInt32(cmd.ExecuteScalar());
        }

        if (total > 0 && total % 30 == 0)
        {
            AnsiConsole.WriteLine();
            var painel = new Panel(new Markup($"[bold gold1]🏆 TROFÉU DESBLOQUEADO![/]\nVocê zerou {total} jogos de [cyan]{plataforma}[/]. Gerando PDF..."))
                .Border(BoxBorder.Double)
                .BorderColor(Spectre.Console.Color.Gold1);
                
            AnsiConsole.Write(painel);
            GerarRelatorioPdf(plataforma, connection);
        }
    }

    static void GerarRelatorioPdf(string plataforma, SqliteConnection connection)
    {
        try
        {
            var pastaRelatorios = Path.Combine(AppContext.BaseDirectory, "trofeus");
            Directory.CreateDirectory(pastaRelatorios);
            
            var nomeArquivo = plataforma.Replace("/", "-").Replace(" ", "_");
            var caminhoArquivo = Path.Combine(pastaRelatorios, $"{nomeArquivo}_expert.pdf");

            // Buscar lista de jogos
            var jogos = new List<Jogo>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM Jogos WHERE Plataforma = $p ORDER BY DataConclusao";
                cmd.Parameters.AddWithValue("$p", plataforma);
                
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    jogos.Add(new Jogo 
                    {
                        Id = reader.GetInt32(0),
                        Nome = reader.GetString(1),
                        Plataforma = reader.GetString(2),
                        Horas = reader.GetInt32(3),
                        DataConclusao = reader.GetString(4)
                    });
                }
            }

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

    static void VerCatalogo()
    {
        AnsiConsole.Clear();
        
        var jogos = new List<Jogo>();
        
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Jogos ORDER BY DataConclusao DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                jogos.Add(new Jogo 
                {
                    Nome = reader.GetString(1),
                    Plataforma = reader.GetString(2),
                    Horas = reader.GetInt32(3)
                });
            }
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Jogo[/]");
        table.AddColumn("[bold]Plataforma[/]");
        table.AddColumn("[bold]Horas[/]");

        foreach (var jogo in jogos)
        {
            table.AddRow(jogo.Nome, jogo.Plataforma, $"{jogo.Horas}h");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[dim]Pressione qualquer tecla para voltar...[/]");
        Console.ReadKey();
    }

    static void VerStatusTropheu()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Progresso por Plataforma:[/]\n");

        var stats = new List<PlataformaStats>();
        
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Plataforma, COUNT(*) as Total FROM Jogos GROUP BY Plataforma";
            using var reader = cmd.ExecuteReader();
            while(reader.Read())
            {
                stats.Add(new PlataformaStats {
                    Plataforma = reader.GetString(0),
                    Total = reader.GetInt32(1)
                });
            }
        }

        foreach (var stat in stats)
        {
            int trofeus = stat.Total / 30;
            
            var bar = new BarChart()
                .Width(60)
                .AddItem(stat.Plataforma, stat.Total, Spectre.Console.Color.Purple);

            AnsiConsole.Write(bar);
            
            if(trofeus > 0)
                AnsiConsole.MarkupLine($"[gold1]   🏆 {trofeus} Troféu(s) conquistado(s)[/] [dim](Total: {stat.Total})[/]");
            else
                AnsiConsole.MarkupLine($"[dim]   {stat.Total}/30 para o primeiro troféu[/]");
                
            AnsiConsole.WriteLine();
        }
        
        AnsiConsole.MarkupLine("\n[dim]Pressione qualquer tecla para voltar...[/]");
        Console.ReadKey();
    }

    class Jogo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string Plataforma { get; set; } = "";
        public int Horas { get; set; }
        public string DataConclusao { get; set; } = "";
    }

    class PlataformaStats
    {
        public string Plataforma { get; set; } = "";
        public int Total { get; set; }
    }
}