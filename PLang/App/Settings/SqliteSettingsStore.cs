using Microsoft.Data.Sqlite;
using App.SafeFileSystem;
using App.Channels.Serializers.Serializer;
using App.Errors;
using App.Variables;
using App.Utility;

namespace App.Settings;

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
    private SqliteSettingsStore(string name)
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
        => new SqliteSettingsStore(name);

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
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<Data> Get<T>(string table, string key) where T : Data
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

            var data = _serializer.Deserialize<T>(result.ToString()!);
            if (data != null) RehydrateValue(data);
            return Task.FromResult((Data?)data ?? Data.Ok(null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                SettingsError.FromException(ex, table, key)));
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
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                SettingsError.FromException(ex, table)));
        }
    }

    public Task<DataList<T>> GetAll<T>(string table) where T : Data
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            var list = new DataList<T>(table);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (raw != null)
                {
                    var data = _serializer.Deserialize<T>(raw);
                    if (data != null)
                    {
                        RehydrateValue(data);
                        list.Add(data);
                    }
                }
            }
            return Task.FromResult(list);
        }
        catch (Exception ex)
        {
            return Task.FromResult(DataList<T>.FromError(
                SettingsError.FromException(ex, table)));
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
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                SettingsError.FromException(ex, table, key)));
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
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                SettingsError.FromException(ex, table, key)));
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
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                SettingsError.FromException(ex, table, key)));
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
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                SettingsError.FromException(ex)));
        }
    }

    /// <summary>
    /// Rehydrates Data.Value from dict/JsonElement to the correct CLR type using Data.Type.
    /// After JSON deserialization, complex objects come back as dictionaries — this converts
    /// them to the registered CLR type so callers get typed values.
    /// </summary>
    private static void RehydrateValue(Data data)
    {
        if (data.Value == null || data.Type == null) return;

        var clrType = TypeMapping.GetType(data.Type.Value);
        if (clrType == null || clrType.IsAssignableFrom(data.Value.GetType())) return;

        var converted = TypeMapping.ConvertTo(data.Value, clrType);
        if (converted != null) data.Value = converted;
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
