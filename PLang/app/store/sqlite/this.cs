using Microsoft.Data.Sqlite;
using app.error;

namespace app.store.sqlite;

/// <summary>
/// A store in SQLite, on disk or in memory.
/// Two-column schema per table: key TEXT PRIMARY KEY, data TEXT (a whole Data in plang's own format).
/// WAL mode for concurrent reads. Each table made by its first statement.
/// Connection per operation (SQLite pools internally via connection string).
/// Born knowing where its database lives, for its life; the database opened at the first verb.
/// </summary>
public sealed class @this : global::app.store.@this
{
    /// <summary>plang's own kind of store, never named by a program as a type.</summary>
    public static bool Internal => true;

    protected override string Kind => "sqlite";

    // Where the database lives: in memory, else in this file.
    private readonly global::app.type.item.path.@this? _file;
    private readonly bool _memory;
    // The in-memory database's name: the store's own, made with it, so no other store reaches it.
    private readonly string _name = "store-" + System.Guid.NewGuid().ToString("N");
    // The store's own context — system-owned. Its result Data is born from it, and its rows are written
    // and read (verified) with it.
    private readonly actor.context.@this Context;

    // The one open, begun by the first verb that reaches the database: the connection string, or the reason it
    // couldn't open.
    private readonly object _gate = new();
    private Task<string>? _opened;
    // An in-memory database lives as long as a connection to it: this one, held for the store's life.
    private SqliteConnection? _sentinel;

    // The database as each verb finds it: blank (in memory, never opened, so nothing written to it) or connected — the
    // connected one made at the first verb that reaches the database.
    private readonly database _blank;
    private database? _connected;
    private database Connected => _connected ?? System.Threading.LazyInitializer.EnsureInitialized(ref _connected, () => new connected(this));

    // A row is a whole Data in plang's own format (application/plang).
    private global::app.type.kind.@this Format => Context.App.type.list["wire"].kind["plang"]!;
    private bool _disposed;

    /// <summary>
    /// A store in <paramref name="file"/>, or in memory, a database of its own, when <paramref name="memory"/>.
    /// Nothing is opened here — the first verb opens it.
    /// </summary>
    public @this(global::app.type.item.path.@this? file, bool memory, actor.context.@this context)
        : base(context.App.type.list[new global::app.type.@this("store", "sqlite"), context])
    {
        _file = file;
        _memory = memory;
        Context = context;
        _blank = new blank(this);
    }

    public override Task<data.@this<T>> Get<T>(string table, string key) => Database.Get<T>(table, key);

    public override Task<data.@this<global::app.type.item.list.@this>> GetAll<T>(string table) => Database.GetAll<T>(table);

    public override Task<data.@this> Set(string table, string key, data.@this data) => Database.Set(table, key, data);

    public override Task<data.@this> Remove(string table, string key) => Database.Remove(table, key);

    public override Task<data.@this<global::app.type.item.@bool.@this>> Exists(string table, string key) => Database.Exists(table, key);

    public override Task<data.@this<global::app.type.item.list.@this>> Tables() => Database.Tables();

    // What the database is now: an in-memory one nothing has opened holds nothing, so it answers empty; once opened,
    // or on disk, the connection.
    private database Database
        => _memory && System.Threading.Volatile.Read(ref _opened) == null ? _blank : Connected;

    // A list of rows, none in it.
    private global::app.type.item.list.@this Empty => new(Context.App.type.list[typeof(global::app.type.item.list.@this)]);

    // The database, opened once — every verb awaits the same open.
    private Task<string> Opened()
    {
        lock (_gate) return _opened ??= Open();
    }

    private async Task<string> Open()
    {
        if (_memory)
        {
            var memory = new SqliteConnectionStringBuilder
            {
                DataSource = _name,
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
        var auth = await _file.Authorize(global::app.type.item.permission.Verb.write, Context);
        if (!auth.Success) throw new global::app.error.AppException(auth.Error!);
        // a folder that can't be made is the open's answer, as a refused file is — never a database that then can't open
        if (_file.Parent is { } parent && await parent.Mkdir(Context) is { Success: false } made)
            throw new global::app.error.AppException(made.Error!);
        // The store's folder is its owner's alone (0700): a key saved in it is no other OS user's to read. Set on the
        // folder, so it holds from the first byte and covers the -wal and -shm files beside it; an older folder is set on
        // its next open. Windows sets nothing (its folders take ACLs), and a mode doesn't survive git or a zip.
        if (_file.Parent is global::app.type.item.path.file.@this folder
            && await folder.Mode((System.IO.UnixFileMode)0, Context) is { Success: false } closed)   // nothing for group or others
            throw new global::app.error.AppException(closed.Error!);

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

    // The database as a verb finds it: each kind answers every verb its own way.
    private abstract class database(@this store)
    {
        private protected @this Store { get; } = store;

        internal abstract Task<data.@this<T>> Get<T>(string table, string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>;
        internal abstract Task<data.@this<global::app.type.item.list.@this>> GetAll<T>(string table) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>;
        internal abstract Task<data.@this> Set(string table, string key, data.@this data);
        internal abstract Task<data.@this> Remove(string table, string key);
        internal abstract Task<data.@this<global::app.type.item.@bool.@this>> Exists(string table, string key);
        internal abstract Task<data.@this<global::app.type.item.list.@this>> Tables();
    }

    // An in-memory database nothing has opened: nothing was written to it, so a read finds nothing and a remove has
    // nothing to take out; a write opens it.
    private sealed class blank(@this store) : database(store)
    {
        internal override Task<data.@this<T>> Get<T>(string table, string key) => Task.FromResult(Store.Context.Ok<T>(default!));

        internal override Task<data.@this<global::app.type.item.list.@this>> GetAll<T>(string table)
            => Task.FromResult(Store.Context.Ok<global::app.type.item.list.@this>(Store.Empty));

        internal override Task<data.@this> Set(string table, string key, data.@this data) => Store.Connected.Set(table, key, data);

        internal override Task<data.@this> Remove(string table, string key) => Task.FromResult(Store.Context.Ok());

        internal override Task<data.@this<global::app.type.item.@bool.@this>> Exists(string table, string key)
            => Task.FromResult(Store.Context.Ok<global::app.type.item.@bool.@this>(false));

        internal override Task<data.@this<global::app.type.item.list.@this>> Tables()
            => Task.FromResult(Store.Context.Ok<global::app.type.item.list.@this>(Store.Empty));
    }

    // The database the store opened (or opens, at this verb): its tables, each a statement's place.
    private sealed class connected(@this store) : database(store)
    {
        // The database's tables by the name a verb gives, each made by its first statement.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, table> _tables = new(StringComparer.OrdinalIgnoreCase);

        private table this[string name] => _tables.GetOrAdd(name, each => new table(Store, each));

        internal override Task<data.@this<T>> Get<T>(string table, string key) => this[table].Get<T>(key);

        internal override Task<data.@this<global::app.type.item.list.@this>> GetAll<T>(string table) => this[table].GetAll<T>();

        internal override Task<data.@this> Set(string table, string key, data.@this data) => this[table].Set(key, data);

        internal override Task<data.@this> Remove(string table, string key) => this[table].Remove(key);

        internal override Task<data.@this<global::app.type.item.@bool.@this>> Exists(string table, string key) => this[table].Exists(key);

        internal override async Task<data.@this<global::app.type.item.list.@this>> Tables()
        {
            try
            {
                var connectionString = await Store.Opened();
                using var connection = new SqliteConnection(connectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

                var tables = Store.Empty;
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    tables.Add(new data.@this("", reader.GetString(0), context: Store.Context));

                return Store.Context.Ok<global::app.type.item.list.@this>(tables);
            }
            catch (global::app.error.AppException ex) { return Store.Context.Error<global::app.type.item.list.@this>(ex.Error); }
            catch (Exception ex)
            {
                return Store.Context.Error<global::app.type.item.list.@this>(
                    SettingsError.FromException(ex));
            }
        }
    }

    // One table of the database: its name as SQLite takes it (letters, digits and underscores, lower case; none left:
    // default_table), and its statements. Its first statement makes it; every statement awaits that one making.
    private sealed class table
    {
        private readonly @this _store;
        // the name the verb gave, for its errors
        private readonly string _given;
        private readonly Lazy<Task<string>> _made;

        internal table(@this store, string name)
        {
            _store = store;
            _given = name;
            var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            Name = string.IsNullOrEmpty(sanitized) ? "default_table" : sanitized.ToLowerInvariant();
            _made = new(Make);
        }

        /// <summary>The table's name as SQLite takes it.</summary>
        internal string Name { get; }

        // The table made in the opened database — the connection string its statements open, once it exists.
        private async Task<string> Make()
        {
            var connectionString = await _store.Opened();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE TABLE IF NOT EXISTS [{Name}] (key TEXT PRIMARY KEY, data TEXT);";
            cmd.ExecuteNonQuery();
            return connectionString;
        }

        internal Task<data.@this<T>> Get<T>(string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
            => Run(key, $"SELECT data FROM [{Name}] WHERE key = @key;", error => _store.Context.Error<T>(error), async cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return _store.Context.Ok<T>(default!);
                return await _store.Hydrate<T>(result.ToString()!);
            });

        internal Task<data.@this<global::app.type.item.list.@this>> GetAll<T>() where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
            => Run(null, $"SELECT key, data FROM [{Name}];", error => _store.Context.Error<global::app.type.item.list.@this>(error), async cmd =>
            {
                // Read all rows first (the reader is sync), then deserialize each — keeps the
                // connection lifetime tight and the await off the open reader.
                var raws = new List<(string Key, string Data)>();
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        if (!reader.IsDBNull(1)) raws.Add((reader.GetString(0), reader.GetString(1)));
                // each row answered under its key — the key it was set under, whatever the Data's own name
                var list = _store.Empty;
                foreach (var (key, raw) in raws)
                {
                    var loaded = await _store.Hydrate<T>(raw);
                    if (loaded.Success && !loaded.Peek().IsNull) list.Add(loaded.Copy(key));
                }
                return _store.Context.Ok<global::app.type.item.list.@this>(list);
            });

        internal Task<data.@this> Set(string key, data.@this data)
            => Run(key, $@"INSERT INTO [{Name}] (key, data) VALUES (@key, @data)
                           ON CONFLICT(key) DO UPDATE SET data = @data;", error => _store.Context.Error(error), async cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                // Store view ships every [Store]-tagged property (incl. [Sensitive] like
                // Identity.PrivateKey). The store owns its TEXT↔stream bridge: serialize to a
                // buffer, bind the TEXT param (this is where the string lives — the column's choice).
                using var ms = new MemoryStream();
                var serialized = await _store.Format.Encode(ms, data, _store.Context, global::app.View.Store);
                if (!serialized.Success) return _store.Context.Error(serialized.Error!);
                cmd.Parameters.AddWithValue("@data", System.Text.Encoding.UTF8.GetString(ms.ToArray()));
                cmd.ExecuteNonQuery();
                return _store.Context.Ok();
            });

        internal Task<data.@this> Remove(string key)
            => Run(key, $"DELETE FROM [{Name}] WHERE key = @key;", error => _store.Context.Error(error), cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                cmd.ExecuteNonQuery();
                return Task.FromResult(_store.Context.Ok());
            });

        internal Task<data.@this<global::app.type.item.@bool.@this>> Exists(string key)
            => Run(key, $"SELECT COUNT(*) FROM [{Name}] WHERE key = @key;", error => _store.Context.Error<global::app.type.item.@bool.@this>(error), cmd =>
            {
                cmd.Parameters.AddWithValue("@key", key);
                var count = Convert.ToInt64(cmd.ExecuteScalar());
                return Task.FromResult(_store.Context.Ok<global::app.type.item.@bool.@this>(count > 0));
            });

        // One statement on this table, once it exists: its connection opened and its command made, the statement's
        // answer, or — when anything fails — the failure in the verb's own shape, naming the table and the key.
        private async Task<TData> Run<TData>(string? key, string text, Func<global::app.error.@this, TData> failed,
            Func<SqliteCommand, Task<TData>> statement)
        {
            try
            {
                using var connection = new SqliteConnection(await _made.Value);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = text;
                return await statement(cmd);
            }
            catch (global::app.error.AppException ex) { return failed(ex.Error); }
            catch (Exception ex) { return failed(SettingsError.FromException(ex, _given, key)); }
        }
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
