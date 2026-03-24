using Microsoft.Data.Sqlite;
using PLang.Interfaces;
using PLang.Runtime2.Engine.Channels.Serializers.Serializer;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.Engine.Settings;

/// <summary>
/// SQLite-backed persistent settings store.
/// Two-column schema per table: key TEXT PRIMARY KEY, data TEXT (Data envelope via PlangSerializer).
/// WAL mode for concurrent reads. Tables auto-created on first write.
/// Connection per operation (SQLite pools internally via connection string).
/// </summary>
public sealed class SqliteSettingsStore : ISettingsStore
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sentinel;
    private readonly PlangSerializer _serializer = new();
    private bool _disposed;

    /// <summary>
    /// Creates a SqliteSettingsStore at the specified database path.
    /// Ensures the parent directory exists using the file system abstraction.
    /// </summary>
    public SqliteSettingsStore(string dbPath, IPLangFileSystem fileSystem)
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

        EnableWalMode();
    }

    /// <summary>
    /// Creates an in-memory SqliteSettingsStore with a sentinel connection that keeps
    /// the database alive for the lifetime of this instance.
    /// </summary>
    private SqliteSettingsStore(string name, bool inMemory)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = name,
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _sentinel = new SqliteConnection(_connectionString);
        _sentinel.Open();
    }

    /// <summary>
    /// Creates an in-memory SQLite settings store. The database lives as long as this instance.
    /// Different names produce isolated databases.
    /// </summary>
    public static SqliteSettingsStore InMemory(string name)
        => new SqliteSettingsStore(name, inMemory: true);

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

            var data = _serializer.Deserialize<Data>(result.ToString()!);
            return Task.FromResult(data ?? Data.Ok(null));
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
                var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (raw != null)
                {
                    var data = _serializer.Deserialize<Data>(raw);
                    if (data != null) items.Add(data);
                }
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

    public Task<Data> Set(string table, string key, Data data)
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
            cmd.Parameters.AddWithValue("@data", _serializer.Serialize(data));
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

        if (_sentinel != null)
        {
            _sentinel.Close();
            _sentinel.Dispose();
        }

        SqliteConnection.ClearPool(new SqliteConnection(_connectionString));
    }
}
