using Microsoft.Data.Sqlite;
using app.type.path;
using app.channel.serializer;
using app.error;
using app.variable;
using app.Utils;

namespace app.module.settings;

/// <summary>
/// SQLite-backed persistent settings store.
/// Two-column schema per table: key TEXT PRIMARY KEY, data TEXT (Data via global::app.channel.serializer.plang.@this).
/// WAL mode for concurrent reads. Tables auto-created on first write.
/// Connection per operation (SQLite pools internally via connection string).
/// </summary>
public sealed class Sqlite : IStore
{
    private readonly string _connectionString;
    private readonly SqliteConnection? _sentinel;
    private readonly global::app.channel.serializer.plang.@this _serializer = new();
    private bool _disposed;

    /// <summary>
    /// Creates a Sqlite at the specified database path, creating the parent
    /// directory if absent. D9b take-over API: sqlite opens the file itself,
    /// so we explicitly Authorize(Write) on the path before handing its
    /// Absolute string to the connection string. Out-of-root paths the
    /// actor hasn't granted bubble up as an exception — sqlite never sees them.
    /// </summary>
    public Sqlite(global::app.type.path.@this dbPath)
    {
        // Take-over API: authorize before passing .Absolute. Sync-wait
        // — Sqlite ctor is sync and the gate is the bootstrap path.
        var verb = new global::app.type.path.permission.verb.@this
        {
            Write = new global::app.type.path.permission.verb.Write()
        };
        var auth = dbPath.Authorize(verb).GetAwaiter().GetResult();
        if (!auth.Success)
            throw new InvalidOperationException(
                $"Sqlite path '{dbPath}' is not authorized for write: {auth.Error?.Message}");

        // Create parent dir via path verb (gated). Mkdir on the parent path
        // — fast-passes in-root, prompts/denies out-of-root (but Authorize
        // above already covered the dbPath's write).
        var parent = dbPath.Parent;
        if (parent != null)
            parent.Mkdir().GetAwaiter().GetResult();

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath.Absolute,
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

            var deserResult = _serializer.Load(result.ToString()!);
            if (!deserResult.Success) return Task.FromResult(app.data.@this.FromError(deserResult.Error!));
            return Task.FromResult((data.@this?)deserResult.Value ?? app.data.@this.Ok(null));
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

            var (typed, derr) = _serializer.Load<T>(result.ToString()!);
            if (derr != null) return Task.FromResult(app.data.@this.FromError(derr));
            if (typed != null) RehydrateValue(typed);
            return Task.FromResult((data.@this?)typed ?? app.data.@this.Ok(null));
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
                    var deserResult = _serializer.Load(raw);
                    if (deserResult.Success && deserResult.Value != null) items.Add((data.@this)deserResult.Value);
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

    public Task<data.@this<global::app.type.list.@this>> GetAll<T>(string table) where T : data.@this
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            var list = new global::app.type.list.@this();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (raw != null)
                {
                    var (loaded, _) = _serializer.Load<T>(raw);
                    if (loaded != null)
                    {
                        RehydrateValue(loaded);
                        list.Add(loaded);
                    }
                }
            }
            return Task.FromResult(data.@this<global::app.type.list.@this>.Ok(list));
        }
        catch (Exception ex)
        {
            return Task.FromResult(data.@this<global::app.type.list.@this>.FromError(
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
            // Local persistence — Store view ships every [Store]-tagged
            // property (including [Sensitive] like Identity.PrivateKey).
            var serialized = _serializer.Store(data);
            if (!serialized.Success) return Task.FromResult(app.data.@this.FromError(serialized.Error!));
            // SQLite is a take-over API — it binds raw CLR values, not born-native
            // wrappers. Collapse an item leaf (text→string) to its raw backing.
            object? dataValue = serialized.Value is global::app.type.item.@this leaf ? leaf.ToRaw() : serialized.Value;
            cmd.Parameters.AddWithValue("@data", dataValue);
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

    public Task<data.@this<global::app.type.@bool.@this>> Exists(string table, string key)
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
            return Task.FromResult(app.data.@this<global::app.type.@bool.@this>.Ok(count > 0));
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this<global::app.type.@bool.@this>.FromError(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<data.@this<global::app.type.list.@this>> Tables()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

            var tables = new global::app.type.list.@this();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                tables.Add(new app.data.@this("", reader.GetString(0)));

            return Task.FromResult(app.data.@this<global::app.type.list.@this>.Ok(tables));
        }
        catch (Exception ex)
        {
            return Task.FromResult(app.data.@this<global::app.type.list.@this>.FromError(
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
        if (data.Value == null || data.Type.IsNull) return;

        // ClrType is non-public on `type.@this` but `internal` — same-assembly
        // callers read directly; the entity owns the registry/primitive
        // fallback chain in one place.
        var clrType = data.Type.ClrType;
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
