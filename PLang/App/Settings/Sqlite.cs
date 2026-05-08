using Microsoft.Data.Sqlite;
using App.FileSystem;
using App.FileSystem.Default;
using App.Channels.Serializers.Serializer;
using App.Errors;
using App.Variables;
using App.Utils;

namespace App.Settings;

/// <summary>
/// SQLite-backed persistent settings store.
/// Two-column schema per table: key TEXT PRIMARY KEY, data TEXT (Data envelope via global::App.Channels.Serializers.Serializer.Plang.@this).
/// WAL mode for concurrent reads. Tables auto-created on first write.
/// Connection per operation (SQLite pools internally via connection string).
/// </summary>
public sealed class Sqlite : IStore
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sentinel;
    private readonly global::App.Channels.Serializers.Serializer.Plang.@this _serializer = new();
    private bool _disposed;

    /// <summary>
    /// Creates a Sqlite at the specified database path.
    /// Ensures the parent directory exists using the file system abstraction.
    /// </summary>
    public Sqlite(string dbPath, IPLangFileSystem fileSystem)
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
    /// Creates an in-memory Sqlite with a sentinel connection that keeps
    /// the database alive for the lifetime of this instance.
    /// </summary>
    private Sqlite(string name)
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
    public static Sqlite InMemory(string name)
        => new Sqlite(name);

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

    public Task<Data.@this> Get(string table, string key)
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
                return Task.FromResult(App.Data.@this.Ok(null));

            var data = _serializer.Deserialize<Data.@this>(result.ToString()!);
            return Task.FromResult(data ?? App.Data.@this.Ok(null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(App.Data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<Data.@this> Get<T>(string table, string key) where T : Data.@this
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
                return Task.FromResult(App.Data.@this.Ok(null));

            var data = _serializer.Deserialize<T>(result.ToString()!);
            if (data != null) RehydrateValue(data);
            return Task.FromResult((Data.@this?)data ?? App.Data.@this.Ok(null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(App.Data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<Data.@this> GetAll(string table)
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            var items = new List<Data.@this>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (raw != null)
                {
                    var data = _serializer.Deserialize<Data.@this>(raw);
                    if (data != null) items.Add(data);
                }
            }
            return Task.FromResult(App.Data.@this.Ok((object)items));
        }
        catch (Exception ex)
        {
            return Task.FromResult(App.Data.@this.FromError(
                SettingsError.FromException(ex, table)));
        }
    }

    public Task<Data.@this<List<T>>> GetAll<T>(string table) where T : Data.@this
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            var list = new List<T>();
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
            return Task.FromResult(Data.@this<List<T>>.Ok(list));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.@this<List<T>>.FromError(
                SettingsError.FromException(ex, table)));
        }
    }

    public Task<Data.@this> Set(string table, string key, Data.@this data)
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

            return Task.FromResult(App.Data.@this.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(App.Data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<Data.@this> Remove(string table, string key)
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

            return Task.FromResult(App.Data.@this.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(App.Data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<Data.@this> Exists(string table, string key)
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
            return Task.FromResult(App.Data.@this.Ok((object)(count > 0)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(App.Data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<Data.@this> Tables()
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

            return Task.FromResult(App.Data.@this.Ok((object)tables));
        }
        catch (Exception ex)
        {
            return Task.FromResult(App.Data.@this.FromError(
                SettingsError.FromException(ex)));
        }
    }

    /// <summary>
    /// Rehydrates Data.Value from dict/JsonElement to the correct CLR type using Data.Type.
    /// After JSON deserialization, complex objects come back as dictionaries — this converts
    /// them to the registered CLR type so callers get typed values.
    /// </summary>
    private static void RehydrateValue(Data.@this data)
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
