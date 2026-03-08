using Microsoft.Data.Sqlite;
using System.Data;

namespace RetroTracker.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
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

    public void AddGame(string nome, string plataforma, int horas, string data)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Jogos (Nome, Plataforma, Horas, DataConclusao) VALUES ($nome, $plataforma, $horas, $data)";
        command.Parameters.AddWithValue("$nome", nome);
        command.Parameters.AddWithValue("$plataforma", plataforma);
        command.Parameters.AddWithValue("$horas", horas);
        command.Parameters.AddWithValue("$data", data);

        command.ExecuteNonQuery();
    }

    public List<Jogo> GetAllGames()
    {
        var jogos = new List<Jogo>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Jogos ORDER BY DataConclusao DESC";
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

        return jogos;
    }

    public List<Jogo> GetGamesByPlatform(string plataforma)
    {
        var jogos = new List<Jogo>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
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

        return jogos;
    }

    public List<PlataformaStats> GetPlatformStats()
    {
        var stats = new List<PlataformaStats>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Plataforma, COUNT(*) as Total FROM Jogos GROUP BY Plataforma";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            stats.Add(new PlataformaStats
            {
                Plataforma = reader.GetString(0),
                Total = reader.GetInt32(1)
            });
        }

        return stats;
    }

    public int GetGameCountByPlatform(string plataforma)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Jogos WHERE Plataforma = $p";
        cmd.Parameters.AddWithValue("$p", plataforma);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}

public class Jogo
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Plataforma { get; set; } = "";
    public int Horas { get; set; }
    public string DataConclusao { get; set; } = "";
}

public class PlataformaStats
{
    public string Plataforma { get; set; } = "";
    public int Total { get; set; }
}