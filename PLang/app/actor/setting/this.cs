using System.Collections.Concurrent;

namespace app.actor.setting;

/// <summary>
/// Settings, in layers: a class's defaults ← the actor's saved row (the user's, else the system's) ←
/// this run's values (<c>set %!x%</c>, the CLI flags) ← the step's own. Each actor has one
/// (<c>app.actor.list.System.Setting</c>, <c>app.actor.list.User.Setting</c>, the user's falling back to the system's) that holds
/// the actor's rows; a context's own layer (<c>context.Setting</c>) chains up to its actor's, so a
/// goal-local value shadows the rest. The rows are read once, when the app starts (<see cref="Load"/>);
/// after that a setting is built in memory. The rows live in the settings' own store,
/// <c>/.data/setting/data.sqlite</c> — never the app's data — made by the root of the chain.
/// </summary>
public sealed class @this : IDisposable
{
    internal const string Table = "settings";                       // the store's table: one row per actor per setting
    private readonly @this? _parent;
    private readonly actor.context.@this _context;                  // born-with-context (not-found Data, the store's reach)
    private readonly ConcurrentDictionary<string, data.@this> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly global::app.actor.@this? _actor;               // an actor's own: the actor whose rows it holds
    private readonly Lazy<Task<ConcurrentDictionary<string, data.@this>>>? _rows;
    private ConcurrentDictionary<string, data.@this>? _held;        // the rows, once read
    private global::app.error.@this? _unread;                       // why the rows could not be read, when they couldn't
    // While the rows are being read, a setting read inside that (the store reading a row back) builds
    // without them — it can't wait on the read it is part of.
    private readonly AsyncLocal<bool> _reading = new();
    // What this scope built, per settings class, with the version it was built at — read-only by contract (a caller
    // that changes a setting changes its Copy). Read from parallel branches, so concurrent.
    private readonly ConcurrentDictionary<System.Type, (long Version, global::app.type.item.setting.@this Built)> _built = new();
    // The settings' version — held by the root of every chain (the system's): any write anywhere moves it, so a
    // built setting knows it is stale whichever scope was written.
    private long _version;

    // The root of the chain — the scope with no parent, whose version every scope reads.
    private @this Root
    {
        get
        {
            var s = this;
            while (s._parent != null) s = s._parent;
            return s;
        }
    }

    // The settings changed somewhere: every setting built before this is stale.
    private void Move() => System.Threading.Interlocked.Increment(ref Root._version);

    // The settings' own store — /.data/setting/data.sqlite, in memory while testing under this app's id — opened by the
    // default store provider, asked by the root of the chain at its first use; every layer reaches it through its root.
    private global::app.store.@this Store => Root._store.Value;
    private readonly Lazy<global::app.store.@this> _store;

    /// <summary>A context's layer, chaining up to <paramref name="parent"/>.</summary>
    public @this(actor.context.@this context, @this? parent = null)
    {
        _context = context;
        _parent = parent;
        _store = new(() =>
        {
            var (provider, error) = context.App.Code.Get<global::app.store.code.IStore>();
            if (provider == null) throw new global::app.error.AppException(error!);
            return provider.Open(global::app.type.item.path.@this.Resolve("/.data/setting/data.sqlite", context),
                () => context.App.Mode.Value == global::app.Mode.Test ? $"setting-{context.App.Id}" : null,
                context);
        });
    }

    /// <summary>The store the root made lets its database go; one never made holds nothing.</summary>
    public void Dispose()
    {
        if (_store.IsValueCreated) _store.Value.Dispose();
    }

    /// <summary>An actor's own: holds the actor's saved rows, falling back to <paramref name="parent"/>
    /// (the system's, for the user).</summary>
    public @this(global::app.actor.@this actor, @this? parent) : this(actor.Context, parent)
    {
        _actor = actor;
        _rows = new(Read);
    }

    /// <summary>Reads the saved rows of every actor up the chain, once — the app does it when it starts.
    /// After it a setting is built in memory. Rows that could not be read are the error (a setting then
    /// builds without them); a save refuses to write over them.</summary>
    public async Task<data.@this> Load()
    {
        for (@this? s = this; s != null; s = s._parent)
            if (s._rows != null && !s._reading.Value && s._held == null)
            {
                s._held = await s._rows.Value;
                // a setting built before the rows were read was built without them
                Move();
            }
        for (@this? s = this; s != null; s = s._parent)
            if (s._unread != null) return _context.Error(s._unread);
        return _context.Ok();
    }

    /// <summary>
    /// An action's option as the settings hold it — the action-param seam's rung and
    /// <c>%!llm.query.setting.cache%</c>'s: this run's value (the action's key <c>llm.query.setting.cache</c>,
    /// then the module's <c>llm.setting.cache</c>), the closest scope first; else the saved rows (the action's
    /// row, then the module's own), the user's before the system's. NotFound when none.
    /// </summary>
    public async ValueTask<data.@this> Get(global::app.goal.step.action.@this action, string option)
    {
        await Load();
        // read at the action's address, then its module's
        var own = action.Address;
        var module = action.Module.Address;
        var run = Run([$"{own}.{option}", $"{module}.{option}"]);
        if (run.IsInitialized) return run;
        if (Saved(own) is { } row && Option(row, option) is { } saved) return saved;
        if (Saved(module) is { } held && Option(held, option) is { } kept) return kept;
        return _context.NotFound($"{own}.{option}");
    }

    // This run's value for the first key that has one, the closest scope first.
    private data.@this Run(string[] keys)
    {
        for (@this? s = this; s != null; s = s._parent)
            foreach (var key in keys)
                if (s._values.TryGetValue(key, out var hit)) return hit;
        return _context.NotFound(keys.Length > 0 ? keys[0] : "setting");
    }

    /// <summary>
    /// This run's value at <paramref name="address"/> in this scope, kept by its address without looking: a setting's
    /// path and then its option (<c>app.channel.setting.format</c>), where the next read of that setting builds it
    /// from. A Data is held whole; null clears it. Checking is the writer's: a step's write goes through its setting
    /// node, the command line through the setting it finds (<see cref="Find"/>, then the setting's own Apply, which
    /// answers where each value is kept).
    /// </summary>
    public data.@this Set(string address, object? value)
    {
        if (value is null) _values.TryRemove(address, out _);
        else _values[address] = value as data.@this ?? new data.@this(address[(address.LastIndexOf('.') + 1)..], value, context: _context);
        Move();
        return value as data.@this ?? _context.Ok();
    }

    /// <summary>
    /// The values a command line's <paramref name="flags"/> set (<c>--test.setting.timeout=5s</c> is the path
    /// <c>test.setting.timeout</c> and its value), each by the address it is kept at — every flag checked by the setting it
    /// names before any is answered. The first a setting refuses (a path no setting goes by, an option it doesn't have, a
    /// value its option can't take) is the answer. Nothing is written: the caller sets them.
    /// </summary>
    public async ValueTask<data.@this<global::app.type.item.dict.@this>> Flags(
        IEnumerable<KeyValuePair<string, object?>> flags, actor.context.@this context)
    {
        var values = new global::app.type.item.dict.@this(_context.App.type.list[typeof(global::app.type.item.dict.@this)]);
        foreach (var (path, value) in flags)
        {
            var setting = Find(path);
            if (!setting.Success) return context.Error<global::app.type.item.dict.@this>(setting.Error!);
            var set = (await setting.Value())!.Apply(path, value, context);
            if (!set.Success) return set;
            foreach (var entry in (await set.Value())!.Entries(context)) values.Set(entry.Name, entry);
        }
        return context.Ok(values);
    }

    /// <summary>
    /// The setting <paramref name="path"/> names: the longest path a setting goes by
    /// (<c>channel.setting.format</c> → <c>app.channel.setting</c>) — a fresh value of it, at its defaults. A path no
    /// setting goes by, or one two settings go by, is refused.
    /// </summary>
    public data.@this<global::app.type.item.setting.@this> Find(string path)
    {
        var answers = _answers ??= Answers();
        for (var end = path.Length; end > 0; end = path.LastIndexOf('.', end - 1))
        {
            if (!answers.TryGetValue(path[..end], out var settings)) continue;
            if (settings.Count > 1)
                return _context.Error<global::app.type.item.setting.@this>(new global::app.error.@this(
                    $"'{path}' names two settings: {string.Join(" and ", settings)} — write the one you mean in full.", "AmbiguousSetting", 400));
            return _context.Ok(Make(settings[0])!);
        }
        return _context.Error<global::app.type.item.setting.@this>(new global::app.error.@this(
            $"'{path}' names no setting — a setting's path is its owner's and then .setting (channel.setting.format).", "UnknownSetting", 400));
    }

    // The settings each path names, as each setting says the paths it goes by — two settings going by one path make
    // it ambiguous.
    private Dictionary<string, List<string>>? _answers;

    private Dictionary<string, List<string>> Answers()
    {
        var answers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Classes().Keys)
            foreach (var key in Make(path)!.Paths)
            {
                if (!answers.TryGetValue(key, out var settings)) answers[key] = settings = [];
                settings.Add(path);
            }
        return answers;
    }

    /// <summary>Stores <paramref name="setting"/> as the row, under its own path, of the actor this scope belongs to:
    /// a settings class whole; at an address no class has (an action's settings, a module's with no class of its own),
    /// this run's values there, as a dict of the options set.</summary>
    public async ValueTask<data.@this> Save(global::app.type.item.setting.@this setting)
    {
        var path = setting.Path;
        var owner = Owner;
        // rows that could not be read are never written over
        if (await owner.Load() is { Success: false } unread) return unread;
        global::app.type.item.@this value = Class(path) != null ? setting
            : global::app.type.item.@this.Create(Under(path), _context);
        var row = new data.@this(path, value, context: _context);
        var stored = await Store.Set(Table, owner._actor!.Row(path), row);
        if (!stored.Success) return stored;
        owner._held![path] = row;
        Move();
        return row;
    }

    /// <summary>Deletes the actor's row for <paramref name="setting"/> — back to the system's row, or the
    /// defaults.</summary>
    public async ValueTask<data.@this> Remove(global::app.type.item.setting.@this setting)
    {
        var path = setting.Path;
        var owner = Owner;
        if (await owner.Load() is { Success: false } unread) return unread;
        var removed = await Store.Remove(Table, owner._actor!.Row(path));
        if (!removed.Success) return removed;
        owner._held!.TryRemove(path, out _);
        Move();
        return removed;
    }

    // The actor's own settings this scope chains up to — the one whose rows a save writes.
    private @this Owner
    {
        get
        {
            for (var s = this; s != null; s = s._parent)
                if (s._actor != null) return s;
            throw new InvalidOperationException("these settings belong to no actor — there is no row to save");
        }
    }

    // The saved row for path, the closest actor first (the user's, then the system's); null when none
    // (or before the rows are read). An actor's own class reads that actor's row alone.
    private data.@this? Saved(string path)
    {
        for (@this? s = this; s != null; s = s._parent)
        {
            if (s._held != null && s._held.TryGetValue(path, out var row)) return row;
            if (s._actor != null && Own(path)) return null;
        }
        return null;
    }

    // Whether the class at path is an actor's own — its values never come from the actor it falls back to.
    private bool Own(string path) => Class(path) is { } clr && System.Attribute.IsDefined(clr, typeof(global::app.type.item.setting.OwnAttribute));

    // One option of a saved row: a setting's (its property), or one an address with no class kept (its entry).
    private data.@this? Option(data.@this row, string name) => row.Peek() switch
    {
        global::app.type.item.setting.@this setting when setting.Property(name) is { } option
            => new data.@this(name, option.GetValue(setting), context: _context),
        global::app.type.item.dict.@this options when options.Entries(_context)
                .FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) is { } entry
            => new data.@this(name, entry.Peek(), context: _context),
        _ => null,
    };

    // The actor's rows, read from the settings' store — each under the key the actor answers for it — each value read
    // through, so a setting builds from them in memory.
    private async Task<ConcurrentDictionary<string, data.@this>> Read()
    {
        var rows = new ConcurrentDictionary<string, data.@this>(StringComparer.OrdinalIgnoreCase);
        _reading.Value = true;
        // A store that can't open (an unwritable root) answers why, as any unreadable rows do: they are unread,
        // not empty — a save or remove, which needs them, answers why (Load too), and nothing writes over rows
        // that may be there.
        var all = await Store.GetAll<global::app.type.item.@this>(Table);
        if (!all.Success)
        {
            await (_context.App.Debug?.Write($"settings: the {_actor.Name} actor's rows could not be read — {all.Error?.Message}") ?? Task.CompletedTask);
            _unread = all.Error ?? new global::app.error.@this($"the {_actor.Name} actor's saved settings could not be read", "SettingsUnreadable", 500);
            return rows;
        }
        foreach (var row in (await all.Value())!.Items(_context))
        {
            if (_actor!.Address(row.Name) is not { } address) continue;
            var value = await row.Value();
            // A row that could NOT be read — an older-shape row 790 drops — is kept WITH its error, never
            // silently skipped: its read named the key and how to fix it (delete it and set it again). It is
            // said on the debug channel now, and held so a read of that setting answers the error (loud), not
            // a silent default. A row that read to nothing (a null value) is simply absent.
            if (!row.Success)
            {
                await (_context.App.Debug?.Write($"settings: '{address}' could not be read — {row.Error?.Message}") ?? Task.CompletedTask);
                rows[address] = row;
            }
            else if (value is { IsNull: false })
                rows[address] = new data.@this(row.Name, value, row.Type, context: _context);
        }
        return rows;
    }

    /// <summary>
    /// The setting class <typeparamref name="T"/> as this scope sees it — its defaults, the actor's saved row on them
    /// (the user's, else the system's), this run's values on top (<c>await context.Setting.Get&lt;test.setting&gt;()</c>).
    /// Read on its first ask and kept until a setting is written; read-only by contract — a caller that changes one
    /// changes its Copy. A saved row that can't be read is loud (790).
    /// </summary>
    public async ValueTask<T> Get<T>() where T : global::app.type.item.setting.@this
        => (T)await Setting(typeof(T));

    /// <summary>The settings <paramref name="host"/> names with <c>ISetting&lt;T&gt;</c> — <c>%!app.goal.list.setting%</c>,
    /// <c>%!app.type.size.setting%</c> — or <paramref name="host"/>'s own, when it is a settings class; null when it
    /// names none. What a value, a type, a host or a concept answers its settings through, by its class.</summary>
    public async ValueTask<data.@this?> Get(System.Type host)
    {
        var @class = Classes().ContainsValue(host) ? host
            : host.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.setting.ISetting<>))
                ?.GetGenericArguments()[0];
        return @class == null ? null : await Get(Address(@class));
    }

    /// <summary>The settings at <paramref name="setting"/>'s address as this scope sees them: the settings class kept there,
    /// as this scope builds it, else <paramref name="setting"/> itself (an action's options, a module's) — what an owner
    /// answers <c>%!llm.query.setting%</c> with.</summary>
    public async ValueTask<data.@this> Get(global::app.type.item.setting.@this setting)
        => Class(setting.Path) != null ? await Get(setting.Path) : new data.@this(setting.Path, setting, context: _context);

    /// <summary>The setting at <paramref name="address"/> as this scope sees it (<c>%!app.test.setting%</c>); NotFound when
    /// no settings class has that address, the error when a value its options can't take is held.</summary>
    public async ValueTask<data.@this> Get(string address)
    {
        if (Class(address) is not { } @class) return _context.NotFound(address);
        try { return new data.@this(address, await Setting(@class), context: _context); }
        catch (global::app.error.AppException ex) { return _context.Error(ex.Error); }
    }

    // The setting of the class, made once per version: after the rows are read, a read is a lookup until a setting is
    // written somewhere (its address is looked up only then).
    private async ValueTask<global::app.type.item.setting.@this> Setting(System.Type @class)
    {
        await Load();
        var version = System.Threading.Interlocked.Read(ref Root._version);
        if (_built.TryGetValue(@class, out var held) && held.Version == version) return held.Built;
        var built = Build(Address(@class));
        _built[@class] = (version, built);
        return built;
    }

    // The setting at path as this scope sees it: the class's defaults ← the saved row ← this run's values.
    private global::app.type.item.setting.@this Build(string path)
    {
        var instance = Make(path) ?? throw new KeyNotFoundException($"'{path}' names no setting class.");
        // a saved row that could NOT be read (an older-shape row) answers its error here — the read of this
        // setting is loud and names the key + how to fix it (790), never a silent fall-back to the defaults
        if (Saved(path) is { Success: false, Error: { } unreadable }) throw new global::app.error.AppException(unreadable);
        // the saved row's options, an owned setting copied — this run's values below never reach the row
        if (Saved(path)?.Peek() is global::app.type.item.setting.@this held)
            foreach (var option in held.Options)
                option.SetValue(instance, option.GetValue(held) is global::app.type.item.setting.@this owned ? owned.Copy() : option.GetValue(held));
        // this run's values for its own options — a longer path under it (llm.query.cache under llm)
        // is another setting's
        var own = Under(path).Where(kv => instance.Property(kv.Key) != null).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var applied = instance.Apply(own, _context);
        if (!applied.Success) throw new global::app.error.AppException(applied.Error!);
        return instance;
    }

    // The settings classes by address, from actor/setting's own index over the settings types (774 A: actor/setting owns
    // the address keys, the type list owns the names — a settings class's name is its namespace, its address its path,
    // and they differ). Built once from the app's types, each class made once here.
    private System.Collections.Generic.Dictionary<string, System.Type>? _classes;
    private System.Type? Class(string address) => Classes().GetValueOrDefault(address);

    /// <summary>Where the settings class <paramref name="class"/> is kept (<c>app.call.setting</c>), or one of its options
    /// (<c>app.call.setting.timing</c>) — known without making one. A class that is no settings class is a program's
    /// mistake.</summary>
    public string Address(System.Type @class, string? option = null)
    {
        // the index's one entry for the class (some thirty classes: a look is cheap)
        var address = Classes().FirstOrDefault(entry => entry.Value == @class).Key
            ?? throw new InvalidOperationException($"{@class.FullName} is not a settings class");
        return option == null ? address : $"{address}.{option}";
    }

    private System.Collections.Generic.Dictionary<string, System.Type> Classes()
    {
        if (_classes != null) return _classes;
        var classes = new System.Collections.Generic.Dictionary<string, System.Type>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var type in _context.App.type.list.Items())
            if (type.ClrType is { } clr && typeof(global::app.type.item.setting.@this).IsAssignableFrom(clr)
                && clr != typeof(global::app.type.item.setting.@this) && !clr.IsAbstract
                && clr.GetConstructor(new[] { typeof(global::app.type.@this) }) != null)
                classes[((global::app.type.item.setting.@this)System.Activator.CreateInstance(clr, _context.App.type.list[clr])!).Path] = clr;
        return _classes = classes;
    }

    // A fresh value of the class at path — its defaults (the empty lists the one apply walk gives a member left
    // out); null when no class there. The value stores no context (Rows takes the asker's).
    private global::app.type.item.setting.@this? Make(string path)
    {
        if (Class(path) is not { } clr) return null;
        var made = (global::app.type.item.setting.@this)System.Activator.CreateInstance(clr, _context.App.type.list[clr])!;
        made.Apply(new Dictionary<string, object?>(), _context);
        return made;
    }

    /// <summary>A fresh value of the settings class a <paramref name="path"/> names — its defaults; null
    /// when no class names it. What the build's step scope teaches from (its Path and its Rows).</summary>
    public global::app.type.item.setting.@this? Fresh(string path) => Make(path);

    // This run's values under path, the closest scope winning, as the options they set — a key deeper than
    // an option (path.llm.system) nests under it. A node set both as a whole and by its members (diff = true,
    // diff.deep = true) keeps its own value as its enabled. An actor's own class stops at that actor's scope.
    private Dictionary<string, object?> Under(string path)
    {
        var under = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var prefix = path + ".";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var own = Own(path);
        const string enabled = "enabled";
        for (@this? s = this; s != null; s = own && s._actor != null ? null : s._parent)
            foreach (var (key, value) in s._values)
            {
                if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !seen.Add(key)) continue;
                var at = under;
                var names = key[prefix.Length..].Split('.');
                for (var i = 0; i < names.Length - 1; i++)
                {
                    if (!at.TryGetValue(names[i], out var inner) || inner is not Dictionary<string, object?> deeper)
                    {
                        deeper = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        if (inner != null) deeper[enabled] = inner;
                        at[names[i]] = deeper;
                    }
                    at = deeper;
                }
                if (at.TryGetValue(names[^1], out var node) && node is Dictionary<string, object?> members)
                    members.TryAdd(enabled, value.Peek());
                else at.TryAdd(names[^1], value.Peek());
            }
        return under;
    }

    /// <summary>An independent copy of this level; keeps the same parent link + context.</summary>
    public @this Clone()
    {
        var clone = new @this(_context, _parent);
        foreach (var kvp in _values) clone._values[kvp.Key] = kvp.Value;
        return clone;
    }
}
