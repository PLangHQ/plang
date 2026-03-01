using System.Text.Json;
using Microsoft.Data.Sqlite;
using PLang.Interfaces;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.DataSource;

/// <summary>
/// SQLite-backed persistent key-value storage.
/// Two-column schema per table: key TEXT PRIMARY KEY, data TEXT (JSON-serialized value).
/// WAL mode for concurrent reads. Tables auto-created on first write.
/// Connection per operation (SQLite pools internally via connection string).
/// </summary>
public sealed class SqliteDataSource : IDataSource
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sentinel;
    private bool _disposed;

    /// <summary>
    /// Creates a SqliteDataSource at the specified database path.
    /// Ensures the parent directory exists using the file system abstraction.
    /// </summary>
    public SqliteDataSource(string dbPath, IPLangFileSystem fileSystem)
    {
        var dir = fileSystem.Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !fileSystem.Directory.Exists(dir))
            fileSystem.Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        // Enable WAL mode on first connection
        EnableWalMode();
    }

    /// <summary>
    /// Creates an in-memory SqliteDataSource with a sentinel connection that keeps
    /// the database alive for the lifetime of this instance.
    /// </summary>
    private SqliteDataSource(string name, bool inMemory)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = name,
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        // Sentinel keeps the in-memory DB alive across connection-per-operation usage
        _sentinel = new SqliteConnection(_connectionString);
        _sentinel.Open();
    }

    /// <summary>
    /// Creates an in-memory SQLite datasource. The database lives as long as this instance.
    /// Different names produce isolated databases.
    /// </summary>
    public static SqliteDataSource InMemory(string name)
        => new SqliteDataSource(name, inMemory: true);

    private void EnableWalMode()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Non-fatal — WAL is a performance optimization, not required
        }
    }

    public Task<Data> Get(string table, string key)
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT data FROM [{SanitizeTableName(table)}] WHERE key = @key;";
            cmd.Parameters.AddWithValue("@key", key);

            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return Task.FromResult(Data.Ok(null));

            var value = DeserializeValue(result.ToString()!);
            return Task.FromResult(Data.Ok(value));
        }
        catch (SqliteException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
    }

    public Task<Data> GetAll(string table)
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            var items = new List<Data>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var k = reader.GetString(0);
                var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                var value = raw != null ? DeserializeValue(raw) : null;
                items.Add(new Data(k, value));
            }
            return Task.FromResult(Data.Ok((object)items));
        }
        catch (SqliteException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table)));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table)));
        }
    }

    public Task<Data> Set(string table, string key, object? value)
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            var sanitized = SanitizeTableName(table);
            cmd.CommandText = $@"INSERT INTO [{sanitized}] (key, data) VALUES (@key, @data)
                                 ON CONFLICT(key) DO UPDATE SET data = @data;";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@data", SerializeValue(value));
            cmd.ExecuteNonQuery();

            return Task.FromResult(Data.Ok());
        }
        catch (SqliteException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(Data.FromError(
                new DataSourceError(ex.Message, "SerializationError", 500)
                {
                    Exception = ex,
                    TableName = table,
                    KeyName = key
                }));
        }
    }

    public Task<Data> Remove(string table, string key)
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM [{SanitizeTableName(table)}] WHERE key = @key;";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.ExecuteNonQuery();

            return Task.FromResult(Data.Ok());
        }
        catch (SqliteException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
    }

    public Task<Data> Exists(string table, string key)
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM [{SanitizeTableName(table)}] WHERE key = @key;";
            cmd.Parameters.AddWithValue("@key", key);

            var count = Convert.ToInt64(cmd.ExecuteScalar());
            return Task.FromResult(Data.Ok((object)(count > 0)));
        }
        catch (SqliteException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex, table, key)));
        }
    }

    public Task<Data> Tables()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

            var tables = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                tables.Add(reader.GetString(0));

            return Task.FromResult(Data.Ok((object)tables));
        }
        catch (SqliteException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex)));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Data.FromError(
                DataSourceError.FromException(ex)));
        }
    }

    private void EnsureTable(string table)
    {
        var sanitized = SanitizeTableName(table);
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS [{sanitized}] (key TEXT PRIMARY KEY, data TEXT);";
        cmd.ExecuteNonQuery();
    }

    private static string SerializeValue(object? value)
    {
        if (value == null) return "null";
        if (value is string s) return JsonSerializer.Serialize(s);
        return JsonSerializer.Serialize(value);
    }

    private static object? DeserializeValue(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "null")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return Data.UnwrapJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            // If it's not valid JSON, return as raw string
            return json;
        }
    }

    /// <summary>
    /// Sanitizes a table name for SQLite.
    /// Allows only alphanumeric and underscores — prevents SQL injection in table names.
    /// </summary>
    private static string SanitizeTableName(string table)
    {
        var sanitized = new string(table.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return string.IsNullOrEmpty(sanitized) ? "default_table" : sanitized.ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Close sentinel first — releases the in-memory DB so it can be garbage collected
        if (_sentinel != null)
        {
            _sentinel.Close();
            _sentinel.Dispose();
        }

        // Clear the connection pool for this data source
        SqliteConnection.ClearPool(new SqliteConnection(_connectionString));
    }
}
