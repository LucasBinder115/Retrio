using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RetroTracker.Services;

namespace RetroTracker;

class Program
{
    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "retrotracker.db");
    private static readonly DatabaseService _dbService;
    private static readonly ReportService _reportService;

    private static readonly string[] Plataformas = new[]
    {
        "NES/Famicom", "Super Nintendo (SNES)", "Mega Drive/Genesis", "PS1", 
        "PS2", "Nintendo 64", "Nintendo DS", "PSP", "GameCube"
    };

    static Program()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _dbService = new DatabaseService(DbPath);
        _reportService = new ReportService();
    }

    static Spectre.Console.Color GetPlatformColor(string plataforma) => plataforma switch
    {
        "PS1" => Spectre.Console.Color.Grey,
        "PS2" => Spectre.Console.Color.Blue,
        "GameCube" => Spectre.Console.Color.Purple,
        "Nintendo 64" => Spectre.Console.Color.Red,
        "NES/Famicom" => Spectre.Console.Color.Orange1,
        "Super Nintendo (SNES)" => Spectre.Console.Color.Green,
        "Mega Drive/Genesis" => Spectre.Console.Color.Yellow,
        "Nintendo DS" => Spectre.Console.Color.Fuchsia,
        "PSP" => Spectre.Console.Color.Aqua,
        _ => Spectre.Console.Color.White
    };

    static string GetPlatformEmoji(string plataforma) => plataforma switch
    {
        "PS1" => "🎮",
        "PS2" => "🎮",
        "GameCube" => "🕹️",
        "Nintendo 64" => "🕹️",
        "NES/Famicom" => "🕹️",
        "Super Nintendo (SNES)" => "🕹️",
        "Mega Drive/Genesis" => "🕹️",
        "Nintendo DS" => "📱",
        "PSP" => "📱",
        _ => "🎮"
    };

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "add" && args.Length >= 4)
        {
            string nome = args[1];
            string plataforma = args[2];
            if (int.TryParse(args[3], out int horas))
            {
                _dbService.AddGame(nome, plataforma, horas, DateTime.Now.ToString("yyyy-MM-dd"));
                VerificarEProgresso(plataforma);
                AnsiConsole.MarkupLine("[green]Jogo adicionado via linha de comando![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Horas inválidas.[/]");
            }
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            Thread.Sleep(500); // Splash screen
            var title = new FigletText("RetroTracker")
                .Centered()
                .Color(Spectre.Console.Color.Purple);
            AnsiConsole.Write(new Panel(title).Border(BoxBorder.Double).BorderColor(Spectre.Console.Color.Gold1));

            var opcao = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Menu Principal[/]")
                    .PageSize(10)
                    .AddChoices(new[] { "Novo Zeramento", "Ver Catálogo", "Procurar Jogo", "Status dos Troféus", "Sair" }));

            switch (opcao)
            {
                case "Novo Zeramento":
                    AdicionarJogo();
                    break;
                case "Ver Catálogo":
                    VerCatalogo();
                    break;
                case "Procurar Jogo":
                    ProcurarJogo();
                    break;
                case "Status dos Troféus":
                    VerStatusTropheu();
                    break;
                case "Sair":
                    return;
            }
        }
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

        _dbService.AddGame(nome, plataforma, horas, data);

        AnsiConsole.MarkupLine($"\n[bold green]✓ Salvo com sucesso![/]");

        VerificarEProgresso(plataforma);

        AnsiConsole.MarkupLine("\n[dim]Pressione qualquer tecla para voltar ao menu...[/]");
        Console.ReadKey();
    }

    static void VerificarEProgresso(string plataforma)
    {
        int total = _dbService.GetGameCountByPlatform(plataforma);

        if (total > 0 && total % 30 == 0)
        {
            AnsiConsole.WriteLine();
            var painel = new Panel(new Markup($"[bold gold1]🏆 TROFÉU DESBLOQUEADO![/]\nVocê zerou {total} jogos de [cyan]{plataforma}[/]. Gerando PDF..."))
                .Border(BoxBorder.Double)
                .BorderColor(Spectre.Console.Color.Gold1);
                
            AnsiConsole.Write(painel);
            GerarRelatorioPdf(plataforma);
        }
    }

    static void GerarRelatorioPdf(string plataforma)
    {
        var jogos = _dbService.GetGamesByPlatform(plataforma);
        _reportService.GenerateTrophyReport(plataforma, jogos);
    }

    static void VerCatalogo()
    {
        AnsiConsole.Clear();
        
        var jogos = _dbService.GetAllGames();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Jogo[/]");
        table.AddColumn("[bold]Plataforma[/]");
        table.AddColumn("[bold]Horas[/]");
        table.AddColumn("[bold]Medalha[/]");

        foreach (var jogo in jogos)
        {
            var color = GetPlatformColor(jogo.Plataforma);
            var emoji = GetPlatformEmoji(jogo.Plataforma);
            var medalha = jogo.Horas <= 2 ? "🏃 Speedrun" : jogo.Horas > 50 ? "🏔️ Maratona" : "";
            table.AddRow($"[{color}]{jogo.Nome}[/]", $"[{color}]{emoji} {jogo.Plataforma}[/]", $"[{color}]{jogo.Horas}h[/]", $"[{color}]{medalha}[/]");
        }

        AnsiConsole.Write(new Panel(table).Header("[bold]Catálogo de Jogos[/]").BorderColor(Spectre.Console.Color.Gold1));
        AnsiConsole.MarkupLine("\n[dim]Pressione qualquer tecla para voltar...[/]");
        Console.ReadKey();
    }

    static void ProcurarJogo()
    {
        AnsiConsole.Clear();
        var search = AnsiConsole.Ask<string>("[bold]Digite parte do nome do jogo:[/] ");
        
        var jogos = _dbService.GetAllGames().Where(j => j.Nome.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (jogos.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Nenhum jogo encontrado.[/]");
        }
        else
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[bold]Jogo[/]");
            table.AddColumn("[bold]Plataforma[/]");
            table.AddColumn("[bold]Horas[/]");
            table.AddColumn("[bold]Medalha[/]");

            foreach (var jogo in jogos)
            {
                var color = GetPlatformColor(jogo.Plataforma);
                var emoji = GetPlatformEmoji(jogo.Plataforma);
                var medalha = jogo.Horas <= 2 ? "🏃 Speedrun" : jogo.Horas > 50 ? "🏔️ Maratona" : "";
                table.AddRow($"[{color}]{jogo.Nome}[/]", $"[{color}]{emoji} {jogo.Plataforma}[/]", $"[{color}]{jogo.Horas}h[/]", $"[{color}]{medalha}[/]");
            }

            AnsiConsole.Write(new Panel(table).Header($"[bold]Resultados para '{search}'[/]").BorderColor(Spectre.Console.Color.Gold1));
        }
        AnsiConsole.MarkupLine("\n[dim]Pressione qualquer tecla para voltar...[/]");
        Console.ReadKey();
    }

    static void VerStatusTropheu()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold]Progresso por Plataforma:[/]\n");

        var stats = _dbService.GetPlatformStats();

        foreach (var stat in stats)
        {
            int trofeus = stat.Total / 30;
            var color = GetPlatformColor(stat.Plataforma);
            var emoji = GetPlatformEmoji(stat.Plataforma);
            
            var bar = new BarChart()
                .Width(60)
                .AddItem($"{emoji} {stat.Plataforma}", stat.Total, color);

            AnsiConsole.Write(bar);
            
            if(trofeus > 0)
                AnsiConsole.MarkupLine($"[{color}]   🏆 {trofeus} Troféu(s) conquistado(s)[/] [dim](Total: {stat.Total})[/]");
            else
                AnsiConsole.MarkupLine($"[{color}]   {stat.Total}/30 para o primeiro troféu[/]");
                
            AnsiConsole.WriteLine();
        }
        
        AnsiConsole.MarkupLine("\n[dim]Pressione qualquer tecla para voltar...[/]");
        Console.ReadKey();
    }
}