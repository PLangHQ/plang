using Microsoft.Data.Sqlite;
using app.types.path;
using app.channels.serializers.serializer;
using app.errors;
using app.variables;
using app.Utils;

namespace app.modules.settings;

/// <summary>
/// SQLite-backed persistent settings store.
/// Two-column schema per table: key TEXT PRIMARY KEY, data TEXT (Data envelope via global::app.channels.serializers.serializer.plang.@this).
/// WAL mode for concurrent reads. Tables auto-created on first write.
/// Connection per operation (SQLite pools internally via connection string).
/// </summary>
public sealed class Sqlite : IStore
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sentinel;
    private readonly global::app.channels.serializers.serializer.plang.@this _serializer = new();
    private bool _disposed;

    /// <summary>
    /// Creates a Sqlite at the specified database path, creating the parent
    /// directory if absent.
    /// </summary>
    public Sqlite(string dbPath)
    {
        var parent = System.IO.Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(parent) && !System.IO.Directory.Exists(parent))
            System.IO.Directory.CreateDirectory(parent);

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
    private Sqlite(string name, bool inMemory)
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
        => new Sqlite(name, inMemory: true);

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

    public Task<data.@this> Get(string table, string key)
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
                return Task.FromResult(app.data.@this.Ok(null));

            var data = _serializer.Deserialize<data.@this>(result.ToString()!);
            return Task.FromResult(data ?? app.data.@this.Ok(null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<data.@this> Get<T>(string table, string key) where T : data.@this
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
                return Task.FromResult(app.data.@this.Ok(null));

            var data = _serializer.Deserialize<T>(result.ToString()!);
            if (data != null) RehydrateValue(data);
            return Task.FromResult((data.@this?)data ?? app.data.@this.Ok(null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<data.@this> GetAll(string table)
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            var items = new List<data.@this>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (raw != null)
                {
                    var data = _serializer.Deserialize<data.@this>(raw);
                    if (data != null) items.Add(data);
                }
            }
            return Task.FromResult(app.data.@this.Ok((object)items));
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this.FromError(
                SettingsError.FromException(ex, table)));
        }
    }

    public Task<data.@this<List<T>>> GetAll<T>(string table) where T : data.@this
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
            return Task.FromResult(data.@this<List<T>>.Ok(list));
        }
        catch (Exception ex)
        {
            return Task.FromResult(data.@this<List<T>>.FromError(
                SettingsError.FromException(ex, table)));
        }
    }

    public Task<data.@this> Set(string table, string key, data.@this data)
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

            return Task.FromResult(app.data.@this.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<data.@this> Remove(string table, string key)
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

            return Task.FromResult(app.data.@this.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<data.@this> Exists(string table, string key)
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
            return Task.FromResult(app.data.@this.Ok((object)(count > 0)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<data.@this> Tables()
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

            return Task.FromResult(app.data.@this.Ok((object)tables));
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this.FromError(
                SettingsError.FromException(ex)));
        }
    }

    /// <summary>
    /// Rehydrates Data.Value from dict/JsonElement to the correct CLR type using Data.Type.
    /// After JSON deserialization, complex objects come back as dictionaries — this converts
    /// them to the registered CLR type so callers get typed values.
    /// </summary>
    private static void RehydrateValue(data.@this data)
    {
        if (data.Value == null || data.Type == null) return;

        var clrType = data.Context?.App.Types.Get(data.Type.Value)
                      ?? AppTypes.GetPrimitiveOrMime(data.Type.Value);
        if (clrType == null || clrType.IsAssignableFrom(data.Value.GetType())) return;

        var converted = AppTypes.ConvertTo(data.Value, clrType);
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
