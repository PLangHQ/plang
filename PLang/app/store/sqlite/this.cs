using Microsoft.Data.Sqlite;
using app.error;

namespace app.store.sqlite;

/// <summary>
/// A store in SQLite, on disk or in memory.
/// Two-column schema per table: key TEXT PRIMARY KEY, data TEXT (a whole Data in plang's own format).
/// WAL mode for concurrent reads. Tables auto-created on first write.
/// Connection per operation (SQLite pools internally via connection string).
/// Born ready: where the database lives is decided, and the database opened, at the first verb.
/// </summary>
public sealed class @this : global::app.store.@this
{
    /// <summary>plang's own kind of store, never named by a program as a type.</summary>
    public static bool Internal => true;

    protected override string Kind => "sqlite";

    // Where the database lives: in memory under the name this answers at the first verb, else in this file.
    private readonly global::app.type.item.path.@this? _file;
    private readonly System.Func<string?> _memory;
    // The store's own context — system-owned. Its result Data is born from it, and its rows are written
    // and read (verified) with it.
    private readonly actor.context.@this Context;

    // The one open, begun by the first verb: the connection string, or the reason it couldn't open.
    private readonly object _gate = new();
    private Task<string>? _opened;
    // An in-memory database lives as long as a connection to it: this one, held for the store's life.
    private SqliteConnection? _sentinel;

    // A row is a whole Data in plang's own format (application/plang).
    private global::app.type.kind.@this Format => Context.App.type.list["wire"].kind["plang"]!;
    private bool _disposed;

    /// <summary>
    /// A store in <paramref name="file"/>, or in memory under the name <paramref name="memory"/> answers when
    /// the store opens (null: the file). Nothing is opened here — the first verb opens it.
    /// </summary>
    public @this(global::app.type.item.path.@this? file, System.Func<string?> memory, actor.context.@this context)
    {
        _file = file;
        _memory = memory;
        Context = context;
    }

    /// <summary>
    /// An in-memory store. The database lives as long as this instance.
    /// Different names produce isolated databases.
    /// </summary>
    public static @this InMemory(string name, actor.context.@this context)
        => new @this(null, () => name, context);

    // The database, opened once — every verb awaits the same open.
    private Task<string> Opened()
    {
        lock (_gate) return _opened ??= Open();
    }

    private async Task<string> Open()
    {
        if (_memory() is { } name)
        {
            var memory = new SqliteConnectionStringBuilder
            {
                DataSource = name,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
            _sentinel = new SqliteConnection(memory);
            _sentinel.Open();
            return memory;
        }
        if (_file == null)
            throw new InvalidOperationException("a sqlite store needs a file or a name in memory");

        // Take-over API: authorize before passing .Absolute. Out-of-root paths the actor hasn't granted
        // fail the open — sqlite never sees them.
        var auth = await _file.Authorize(global::app.type.item.permission.Verb.Write, Context);
        if (!auth.Success)
            throw new InvalidOperationException(
                $"Sqlite path '{_file}' is not authorized for write: {auth.Error?.Message}");
        if (_file.Parent is { } parent)
            await parent.Mkdir(Context);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _file.Absolute,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        EnableWalMode(connectionString);
        return connectionString;
    }

    // WAL journaling. A database that can't take it answers its mode (in memory: "memory") without throwing,
    // so a throw here is the database failing, and it bubbles.
    private void EnableWalMode(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
    }

    // The store persists TEXT, the format speaks bytes — so the store owns its own TEXT↔bytes bridge (it
    // chose the column type). Read: a stored string → a Data<T>, a typed face over the Data as read.
    private async Task<data.@this<T>> Hydrate<T>(string stored) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var read = await Format.Decode(System.Text.Encoding.UTF8.GetBytes(stored), Context, view: global::app.View.Store);
        return read.Success ? read.As<T>() : global::app.data.@this<T>.From(read);
    }

    public override async Task<data.@this<T>> Get<T>(string table, string key)
    {
        try
        {
            var connectionString = await Opened();
            EnsureTable(connectionString, table);
            using var connection = new SqliteConnection(connectionString);
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

    public override async Task<data.@this<global::app.type.item.list.@this>> GetAll<T>(string table)
    {
        try
        {
            var connectionString = await Opened();
            EnsureTable(connectionString, table);
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT key, data FROM [{SanitizeTableName(table)}];";

            // Read all rows first (the reader is sync), then deserialize each — keeps the
            // connection lifetime tight and the await off the open reader.
            var raws = new List<string>();
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    if (!reader.IsDBNull(1)) raws.Add(reader.GetString(1));
            var list = new global::app.type.item.list.@this();
            foreach (var raw in raws)
            {
                var loaded = await Hydrate<T>(raw);
                if (loaded.Success && !loaded.Peek().IsNull) list.Add(loaded);
            }
            return Context.Ok<global::app.type.item.list.@this>(list);
        }
        catch (Exception ex)
        {
            return Context.Error<global::app.type.item.list.@this>(
                SettingsError.FromException(ex, table));
        }
    }

    public override async Task<data.@this> Set(string table, string key, data.@this data)
    {
        try
        {
            var connectionString = await Opened();
            EnsureTable(connectionString, table);
            using var connection = new SqliteConnection(connectionString);
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
            var serialized = await Format.Encode(ms, data, Context, global::app.View.Store);
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

    public override async Task<data.@this> Remove(string table, string key)
    {
        try
        {
            var connectionString = await Opened();
            EnsureTable(connectionString, table);
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM [{SanitizeTableName(table)}] WHERE key = @key;";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.ExecuteNonQuery();

            return Context.Ok();
        }
        catch (Exception ex)
        {
            return Context.Error(
                SettingsError.FromException(ex, table, key));
        }
    }

    public override async Task<data.@this<global::app.type.item.@bool.@this>> Exists(string table, string key)
    {
        try
        {
            var connectionString = await Opened();
            EnsureTable(connectionString, table);
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM [{SanitizeTableName(table)}] WHERE key = @key;";
            cmd.Parameters.AddWithValue("@key", key);

            var count = Convert.ToInt64(cmd.ExecuteScalar());
            return Context.Ok<global::app.type.item.@bool.@this>(count > 0);
        }
        catch (Exception ex)
        {
            return Context.Error<global::app.type.item.@bool.@this>(
                SettingsError.FromException(ex, table, key));
        }
    }

    public override async Task<data.@this<global::app.type.item.list.@this>> Tables()
    {
        try
        {
            var connectionString = await Opened();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

            var tables = new global::app.type.item.list.@this();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                tables.Add(new data.@this("", reader.GetString(0), context: Context));

            return Context.Ok<global::app.type.item.list.@this>(tables);
        }
        catch (Exception ex)
        {
            return Context.Error<global::app.type.item.list.@this>(
                SettingsError.FromException(ex));
        }
    }

    private void EnsureTable(string connectionString, string table)
    {
        var sanitized = SanitizeTableName(table);
        using var connection = new SqliteConnection(connectionString);
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

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_sentinel != null)
        {
            _sentinel.Close();
            _sentinel.Dispose();
        }

        // A store that opened lets its database go; one that never opened holds nothing.
        if (_opened is { IsCompletedSuccessfully: true } opened)
            SqliteConnection.ClearPool(new SqliteConnection(opened.Result));
    }
}
