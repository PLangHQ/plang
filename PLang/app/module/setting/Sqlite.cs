using Microsoft.Data.Sqlite;
using app.type.path;
using app.channel.serializer;
using app.error;
using app.variable;
using app.Utils;

namespace app.module.setting;

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
    // The settings store is application/plang by construction and system-owned:
    // it serializes through the system context so reads verify and route through
    // the typed wire reader (no context-less narrow).
    private readonly global::app.channel.serializer.plang.@this _serializer;
    // The store's own context — system-owned. Its result Data is born from it.
    private readonly actor.context.@this Context;
    private bool _disposed;

    /// <summary>
    /// Builds a Sqlite over an already-authorized connection string. The async
    /// gate work (Authorize + parent Mkdir) happens in <see cref="CreateAsync"/>
    /// — the ctor itself does no I/O await, so it never sync-waits.
    /// </summary>
    private Sqlite(string connectionString, actor.context.@this context)
    {
        _serializer = new(context);
        Context = context;
        _connectionString = connectionString;
        EnableWalMode();
    }

    /// <summary>
    /// Creates a Sqlite at the specified database path, creating the parent
    /// directory if absent. D9b take-over API: sqlite opens the file itself,
    /// so we explicitly Authorize(Write) on the path before handing its
    /// Absolute string to the connection string. Out-of-root paths the
    /// actor hasn't granted bubble up as an exception — sqlite never sees them.
    /// Async all the way: no <c>GetAwaiter().GetResult()</c>, so parallel store
    /// construction never starves the threadpool.
    /// </summary>
    public static async Task<Sqlite> CreateAsync(global::app.type.path.@this dbPath, actor.context.@this context)
    {
        // Take-over API: authorize before passing .Absolute.
        var auth = await dbPath.Authorize(global::app.type.permission.Verb.Write);
        if (!auth.Success)
            throw new InvalidOperationException(
                $"Sqlite path '{dbPath}' is not authorized for write: {auth.Error?.Message}");

        // Create parent dir via path verb (gated). Mkdir on the parent path
        // — fast-passes in-root, prompts/denies out-of-root (but Authorize
        // above already covered the dbPath's write).
        var parent = dbPath.Parent;
        if (parent != null)
            await parent.Mkdir();

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath.Absolute,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        return new Sqlite(connectionString, context);
    }

    /// <summary>
    /// Creates an in-memory Sqlite with a sentinel connection that keeps
    /// the database alive for the lifetime of this instance.
    /// </summary>
    private Sqlite(string name, bool inMemory, actor.context.@this context)
    {
        _serializer = new(context);
        Context = context;
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
    public static Sqlite InMemory(string name, actor.context.@this context)
        => new Sqlite(name, inMemory: true, context);

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
                return Context.Ok<T>(default!);
            return await Hydrate<T>(result.ToString()!);
        }
        catch (Exception ex)
        {
            return Context.Error<T>(SettingsError.FromException(ex, table, key));
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
            var list = new global::app.type.list.@this(Context);
            foreach (var raw in raws)
            {
                var loaded = await Hydrate<T>(raw);
                if (loaded.Success && !loaded.Peek().IsNull) list.Add(loaded);
            }
            return Context.Ok<global::app.type.list.@this>(list);
        }
        catch (Exception ex)
        {
            return Context.Error<global::app.type.list.@this>(
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
            if (!serialized.Success) return Context.Error(serialized.Error!);
            cmd.Parameters.AddWithValue("@data", System.Text.Encoding.UTF8.GetString(ms.ToArray()));
            cmd.ExecuteNonQuery();

            return Context.Ok();
        }
        catch (Exception ex)
        {
            return Context.Error(
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

            return Task.FromResult(Context.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Context.Error(
                SettingsError.FromException(ex, table, key)));
        }
    }

    public Task<data.@this<global::app.type.item.@bool.@this>> Exists(string table, string key)
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
            return Task.FromResult(Context.Ok<global::app.type.item.@bool.@this>(count > 0));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Context.Error<global::app.type.item.@bool.@this>(
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

            var tables = new global::app.type.list.@this(Context);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                tables.Add(new data.@this("", reader.GetString(0), context: Context));

            return Task.FromResult(Context.Ok<global::app.type.list.@this>(tables));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Context.Error<global::app.type.list.@this>(
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
