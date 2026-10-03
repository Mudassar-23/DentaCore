using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DentaCore.Infrastructure.Persistence;

public static class DatabaseConnectionHelper
{
    /// <summary>
    /// Converts a PostgreSQL URI (e.g., postgresql://user:pass@host:port/dbname)
    /// or standard ADO.NET connection string into a valid Npgsql connection string.
    /// </summary>
    public static string ToPostgreSqlConnectionString(string? urlOrConn)
    {
        if (string.IsNullOrWhiteSpace(urlOrConn))
            return string.Empty;

        var trimmed = urlOrConn.Trim();

        // If not a URI, assume it is already a key-value ADO.NET connection string
        if (!trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        try
        {
            var uri = new Uri(trimmed);
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = string.IsNullOrWhiteSpace(database) ? "postgres" : database,
                Username = username,
                Password = password
            };

            // Parse optional query parameters (e.g. ?sslmode=prefer&pooling=true)
            if (!string.IsNullOrWhiteSpace(uri.Query))
            {
                var query = uri.Query.TrimStart('?');
                foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        var key = Uri.UnescapeDataString(kv[0]).Trim();
                        var val = Uri.UnescapeDataString(kv[1]).Trim();

                        if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Enum.TryParse<SslMode>(val, true, out var sslMode))
                                builder.SslMode = sslMode;
                        }
                        else
                        {
                            try { builder[key] = val; } catch { /* Ignore unknown options */ }
                        }
                    }
                }
            }

            return builder.ConnectionString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database] Warning: Failed to parse PostgreSQL URI '{urlOrConn}': {ex.Message}. Using as raw string.");
            return trimmed;
        }
    }

    /// <summary>
    /// Normalizes SQLite connection string from URI (e.g., sqlite:///./data/ai.db)
    /// or raw path/format into 'Data Source=...' format and ensures directory exists.
    /// </summary>
    public static string NormalizeSqliteConnectionString(string? urlOrConn)
    {
        if (string.IsNullOrWhiteSpace(urlOrConn))
            urlOrConn = "Data Source=dentacore.db";

        var trimmed = urlOrConn.Trim();
        string finalConn;

        if (trimmed.StartsWith("sqlite:///", StringComparison.OrdinalIgnoreCase))
        {
            var path = trimmed["sqlite:///".Length..];
            finalConn = $"Data Source={path}";
        }
        else if (trimmed.StartsWith("sqlite://", StringComparison.OrdinalIgnoreCase))
        {
            var path = trimmed["sqlite://".Length..];
            finalConn = $"Data Source={path}";
        }
        else if (!trimmed.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            finalConn = $"Data Source={trimmed}";
        }
        else
        {
            finalConn = trimmed;
        }

        EnsureSqliteDirectoryExists(finalConn);
        return finalConn;
    }

    /// <summary>
    /// Ensures that any subdirectory path specified in the SQLite Data Source exists.
    /// </summary>
    public static void EnsureSqliteDirectoryExists(string connectionString)
    {
        try
        {
            var match = Regex.Match(
                connectionString,
                @"Data Source\s*=\s*([^;]+)",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var filePath = match.Groups[1].Value.Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(filePath) && filePath != ":memory:")
                {
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        Console.WriteLine($"[Database] Created SQLite data directory: {dir}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database] Warning: Failed to check/create SQLite directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts a quick connection to PostgreSQL to check reachability and authentication.
    /// Returns true if PostgreSQL server is reachable.
    /// If PostgreSQL is down or unreachable, returns false (allowing fallback to SQLite).
    /// </summary>
    public static bool CanConnectToPostgreSql(string connectionString, out string? errorMessage, int timeoutSeconds = 3)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errorMessage = "PostgreSQL connection string is empty.";
            return false;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = timeoutSeconds,
                CommandTimeout = timeoutSeconds
            };

            using var conn = new NpgsqlConnection(builder.ConnectionString);
            conn.Open();
            return true;
        }
        catch (PostgresException pgEx)
        {
            // Error 3D000 = database does not exist yet. Server is UP and working!
            if (pgEx.SqlState == "3D000")
            {
                return true;
            }

            // Authentication failure or permission issues
            errorMessage = $"PostgreSQL authentication/database error: {pgEx.MessageText} (SQLState: {pgEx.SqlState})";
            return false;
        }
        catch (SocketException sockEx)
        {
            errorMessage = $"PostgreSQL host unreachable: {sockEx.Message}";
            return false;
        }
        catch (NpgsqlException npgEx)
        {
            errorMessage = $"PostgreSQL connection failed: {npgEx.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Unexpected connection check failure: {ex.Message}";
            return false;
        }
    }
}
