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
        var auth = dbPath.Authorize(global::app.type.permission.Verb.Write).GetAwaiter().GetResult();
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

    // The store persists TEXT, the serializer speaks streams — so the store owns its own
    // TEXT↔stream bridge (it chose the column type). Read: a stored string → a Data<T>.
    private async Task<data.@this<T>> Hydrate<T>(string stored) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stored));
        return await _serializer.DeserializeAsync<T>(ms, global::app.View.Store);
    }

    public async Task<data.@this<T>> Get<T>(string table, string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
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
                return data.@this<T>.Ok(default!);
            return await Hydrate<T>(result.ToString()!);
        }
        catch (Exception ex)
        {
            return data.@this<T>.FromError(SettingsError.FromException(ex, table, key));
        }
    }

    public async Task<data.@this<global::app.type.list.@this>> GetAll<T>(string table) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        try
        {
            EnsureTable(table);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            // Read all rows first (the reader is sync), then deserialize each — keeps the
            // connection lifetime tight and the await off the open reader.
            var raws = new List<string>();
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    if (!reader.IsDBNull(1)) raws.Add(reader.GetString(1));
            var list = new global::app.type.list.@this();
            foreach (var raw in raws)
            {
                var loaded = await Hydrate<T>(raw);
                if (loaded.Success && !loaded.Peek().IsNull) list.Add(loaded);
            }
            return data.@this<global::app.type.list.@this>.Ok(list);
        }
        catch (Exception ex)
        {
            return data.@this<global::app.type.list.@this>.FromError(
                SettingsError.FromException(ex, table));
        }
    }

    public async Task<data.@this> Set(string table, string key, data.@this data)
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
            // Store view ships every [Store]-tagged property (incl. [Sensitive] like
            // Identity.PrivateKey). The store owns its TEXT↔stream bridge: serialize to a
            // buffer, bind the TEXT param (this is where the string lives — the column's choice).
            using var ms = new MemoryStream();
            var serialized = await _serializer.SerializeAsync(ms, data, global::app.View.Store);
            if (!serialized.Success) return app.data.@this.FromError(serialized.Error!);
            cmd.Parameters.AddWithValue("@data", System.Text.Encoding.UTF8.GetString(ms.ToArray()));
            cmd.ExecuteNonQuery();

            return app.data.@this.Ok();
        }
        catch (Exception ex)
        {
            return app.data.@this.FromError(
                SettingsError.FromException(ex, table, key));
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
