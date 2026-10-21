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
    // What this scope built, per setting path, with the version it was built at — read-only by contract (a caller
    // that changes a setting changes its Copy). Read from parallel branches, so concurrent.
    private readonly ConcurrentDictionary<string, (long Version, global::app.type.item.setting.@this Built)> _built =
        new(StringComparer.OrdinalIgnoreCase);
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

    // The settings' own store — /.data/setting/data.sqlite, in memory while testing under this app's id — made by the
    // root of the chain at its first use; every layer reaches it through its root.
    private global::app.store.@this Store => Root._store.Value;
    private readonly Lazy<global::app.store.@this> _store;

    /// <summary>A context's layer, chaining up to <paramref name="parent"/>.</summary>
    public @this(actor.context.@this context, @this? parent = null)
    {
        _context = context;
        _parent = parent;
        _store = new(() => new global::app.store.sqlite.@this(
            global::app.type.item.path.@this.Resolve("/.data/setting/data.sqlite", context),
            () => context.App.Mode.Value == global::app.Mode.Test ? $"setting-{context.App.Id}" : null,
            context));
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
        // read where each node writes: under the action's settings' path, then its module's
        var settings = new global::app.type.item.setting.action.@this(action);
        var own = settings.Path;
        var module = settings.Module.Path;
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

    /// <summary>This run's value for <paramref name="key"/> in this scope, as a setting node wrote it once it checked
    /// it against its own options (an action's, a module's, a class's); null clears it.</summary>
    internal ValueTask<data.@this> Write(string key, data.@this? value)
    {
        if (value is null) _values.TryRemove(key, out _);
        else _values[key] = value;
        Move();
        return new(value ?? _context.Ok());
    }

    /// <summary>
    /// This run's value at <paramref name="path"/> — a setting's path (<c>app.</c> left out or not) and then its option
    /// (<c>channel.setting.format</c>, <c>call.setting.diff.deep</c>), or the setting's alone with a dict of its
    /// options (<c>test.setting={"timeout":"5s"}</c>). What the command line writes (<c>--&lt;path&gt;=&lt;value&gt;</c>).
    /// A path no setting answers to, an option the setting doesn't have, or a value its option can't take is refused —
    /// nothing is written then. A Data value is held whole. Answers the setting written, at its defaults.
    /// </summary>
    public data.@this Set(string path, object? value)
    {
        var found = Find(path);
        if (!found.Success) return found;
        var setting = ((global::app.type.item.setting.@this)found.Peek()!).Path;
        var option = path.Length > found.Name.Length ? path[(found.Name.Length + 1)..] : null;
        var raw = value is data.@this given ? given.Peek() : value;
        var options = option == null ? raw as IDictionary<string, object?> : Nested(option, raw);
        if (options == null)
            return _context.Error(new global::app.error.@this(
                $"'{path}' is the setting {setting}: give it its options, {{\"option\": value}}, or name one ({path}.<option>).",
                "TypeConversionFailed", 400));
        var fits = Make(setting)!.Apply(options, _context);
        if (!fits.Success) return fits;
        if (option != null && value is data.@this whole) _values[$"{setting}.{option}"] = whole;
        else Flatten(setting, options);
        Move();
        return found;
    }

    /// <summary>
    /// The setting <paramref name="path"/> names, as the command line names it: the longest path a setting answers to,
    /// its own or without <c>app.</c> (<c>channel.setting.format</c> → <c>app.channel.setting</c>) — a fresh value of
    /// it, at its defaults, the Data named by the part of the path it answered to. A path no setting answers to, or
    /// one two settings answer to, is refused.
    /// </summary>
    public data.@this Find(string path)
    {
        var flags = _flags ??= Flags();
        for (var end = path.Length; end > 0; end = path.LastIndexOf('.', end - 1))
        {
            if (!flags.TryGetValue(path[..end], out var settings)) continue;
            if (settings.Count > 1)
                return _context.Error(new global::app.error.@this(
                    $"'{path}' names two settings: {string.Join(" and ", settings)} — write the one you mean in full.", "AmbiguousSetting", 400));
            return new data.@this(path[..end], Make(settings[0]), context: _context);
        }
        return _context.Error(new global::app.error.@this(
            $"'{path}' names no setting — a setting's path is its owner's and then .setting (channel.setting.format).", "UnknownSetting", 400));
    }

    // Each setting by the paths a flag names it: its own, and without `app.` (app.channel.setting, channel.setting).
    private Dictionary<string, List<string>>? _flags;

    private Dictionary<string, List<string>> Flags()
    {
        var flags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        const string app = "app.";
        foreach (var path in (_classes ??= Index()).Keys)
            foreach (var key in path.StartsWith(app, StringComparison.OrdinalIgnoreCase) ? new[] { path, path[app.Length..] } : new[] { path })
            {
                if (!flags.TryGetValue(key, out var paths)) flags[key] = paths = [];
                paths.Add(path);
            }
        return flags;
    }

    // An option path's value as the options it sets: format → {format: v}, diff.deep → {diff: {deep: v}}.
    private IDictionary<string, object?> Nested(string option, object? value)
    {
        var names = option.Split('.');
        IDictionary<string, object?> options = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { [names[^1]] = value };
        for (var i = names.Length - 2; i >= 0; i--)
            options = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { [names[i]] = options };
        return options;
    }

    private void Flatten(string path, IDictionary<string, object?> values)
    {
        foreach (var (key, value) in values)
        {
            if (value is IDictionary<string, object?> nested) Flatten($"{path}.{key}", nested);
            else _values[$"{path}.{key}"] = new data.@this(key, value, context: _context);
        }
    }

    /// <summary>Stores <paramref name="setting"/> whole as the row, under its own path, of the actor this
    /// scope belongs to.</summary>
    public async ValueTask<data.@this> Save(global::app.type.item.setting.@this setting)
    {
        var path = setting.Path;
        // a node's settings (%!http.setting%, %!llm.query.setting%) are no class: its row could not be read back.
        // An action's settings are not storable yet; when they are, their row is a dict of the set options,
        // read by Option(), and Option()'s action-row branch goes.
        if (Class(path) == null)
            return _context.Error(new global::app.error.@this(
                $"'{path}' is not a setting class — save the class that holds the option.", "NotASettingClass", 400));
        var owner = Owner;
        // rows that could not be read are never written over
        if (await owner.Load() is { Success: false } unread) return unread;
        var row = new data.@this($"{owner._actor!.Name.ToLowerInvariant()}!{path}", setting, context: _context);
        var stored = await Store.Set(Table, row.Name, row);
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
        var removed = await Store.Remove(Table, $"{owner._actor!.Name.ToLowerInvariant()}!{path}");
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

    // One option of a saved row: a setting's (its property), or an action's (its Property row).
    private data.@this? Option(data.@this row, string name) => row.Peek() switch
    {
        global::app.type.item.setting.@this setting when setting.Option(name) is { } option
            => new data.@this(name, option.GetValue(setting), context: _context),
        global::app.goal.step.action.@this action
            when action.Property.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { } property
            => new data.@this(name, property.Value, context: _context),
        _ => null,
    };

    // The actor's rows, read from the settings' store — its keys are "<actor>!<path>" — each value read
    // through, so a setting builds from them in memory.
    private async Task<ConcurrentDictionary<string, data.@this>> Read()
    {
        var rows = new ConcurrentDictionary<string, data.@this>(StringComparer.OrdinalIgnoreCase);
        var prefix = _actor!.Name.ToLowerInvariant() + "!";
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
            if (!row.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var key = row.Name[prefix.Length..];
            var value = await row.Value();
            // A row that could NOT be read — an older-shape row 790 drops — is kept WITH its error, never
            // silently skipped: its read named the key and how to fix it (delete it and set it again). It is
            // said on the debug channel now, and held so a read of that setting answers the error (loud), not
            // a silent default. A row that read to nothing (a null value) is simply absent.
            if (!row.Success)
            {
                await (_context.App.Debug?.Write($"settings: '{key}' could not be read — {row.Error?.Message}") ?? Task.CompletedTask);
                rows[key] = row;
            }
            else if (value is { IsNull: false })
                rows[key] = new data.@this(row.Name, value, row.Type, context: _context);
        }
        return rows;
    }

    /// <summary>
    /// The setting class at <paramref name="path"/>, as this scope sees it — its defaults, the actor's
    /// saved row on them (the user's, else the system's; none before the app reads them), this run's
    /// values on top. What an owner reads its settings through (<c>test.start</c>, the build, Debug).
    /// </summary>
    public global::app.type.item.setting.@this this[string path]
    {
        get
        {
            // built once per version: a read is a lookup until a setting is written somewhere (read-only by
            // contract — a caller that changes one changes its Copy)
            var version = System.Threading.Interlocked.Read(ref Root._version);
            if (_built.TryGetValue(path, out var held) && held.Version == version) return held.Built;
            var built = Build(path);
            _built[path] = (version, built);
            return built;
        }
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
        var own = Under(path).Where(kv => instance.Option(kv.Key) != null).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var applied = instance.Apply(own, _context);
        if (!applied.Success) throw new global::app.error.AppException(applied.Error!);
        return instance;
    }

    /// <summary>The setting class <typeparamref name="T"/>, as this scope sees it — the class says its own
    /// path (<c>context.Setting.Of&lt;test.setting&gt;()</c>).</summary>
    public T Of<T>() where T : global::app.type.item.setting.@this
        => (T)this[((T)System.Activator.CreateInstance(typeof(T), _context.App.type.list[typeof(T)])!).Path];

    /// <summary>The settings <paramref name="owner"/> names with <c>ISetting&lt;T&gt;</c>, as this scope sees
    /// them — <c>%!app.goal.list.setting%</c>, <c>%!app.setting%</c>; null when it names none.</summary>
    public ValueTask<data.@this?> Of(object owner) => Named(owner.GetType());

    /// <summary>The settings <paramref name="type"/>'s class names — <c>%!app.type.size.setting%</c> is the size
    /// class's; null when it names none.</summary>
    public ValueTask<data.@this?> Of(global::app.type.@this type)
        => type.ClrType is { } clr ? Named(clr) : ValueTask.FromResult<data.@this?>(null);

    // The settings the class host names with ISetting<T>, as this scope sees them.
    private async ValueTask<data.@this?> Named(System.Type host)
    {
        var named = host.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.setting.ISetting<>));
        return named == null ? null : await Of(named.GetGenericArguments()[0]);
    }

    /// <summary>The setting class <paramref name="class"/>, as this scope sees it — <c>%!app.test.setting%</c>,
    /// the settings a concept's type answers.</summary>
    public async ValueTask<data.@this> Of(System.Type @class)
    {
        await Load();
        return Instance(((global::app.type.item.setting.@this)Activator.CreateInstance(@class, _context.App.type.list[@class])!).Path);
    }

    // The settings class at path, from actor/setting's own path→class index over the settings types
    // (774 A: actor/setting owns the path keys, the type list owns the names — a settings class's name is
    // its namespace, its path its address, and they differ). Built once from the app's types.
    private System.Collections.Generic.Dictionary<string, System.Type>? _classes;
    private System.Type? Class(string path) => (_classes ??= Index()).GetValueOrDefault(path);

    private System.Collections.Generic.Dictionary<string, System.Type> Index()
    {
        var map = new System.Collections.Generic.Dictionary<string, System.Type>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var type in _context.App.type.list.Items())
            if (type.ClrType is { } clr && typeof(global::app.type.item.setting.@this).IsAssignableFrom(clr)
                && clr != typeof(global::app.type.item.setting.@this) && !clr.IsAbstract
                && clr.GetConstructor(new[] { typeof(global::app.type.@this) }) != null)
                map[((global::app.type.item.setting.@this)System.Activator.CreateInstance(clr, _context.App.type.list[clr])!).Path] = clr;
        return map;
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

    // The class at path as a value — a value its options can't take (this run's) is the error.
    private data.@this Instance(string path)
    {
        try { return new data.@this(path, this[path], context: _context); }
        catch (global::app.error.AppException ex) { return _context.Error(ex.Error); }
    }

    /// <summary>
    /// <paramref name="module"/>'s settings as this scope sees them — <c>%!llm.setting%</c>, <c>%!http.setting%</c>:
    /// its own class when it has one, else the module's node, whose options are ones its actions take.
    /// </summary>
    public async ValueTask<data.@this> Of(global::app.module.@this module)
    {
        await Load();
        var node = new global::app.type.item.setting.module.@this(module.Name,
            _context.App.type.list[typeof(global::app.type.item.setting.module.@this)]);
        return Class(node.Path) != null ? Instance(node.Path) : new data.@this(node.Path, node, context: _context);
    }

    /// <summary>
    /// <paramref name="action"/>'s settings as this scope sees them — <c>%!llm.decider.setting%</c>,
    /// <c>%!llm.query.setting%</c>: its own class when it has one, else the action's node, whose options are its
    /// properties.
    /// </summary>
    public async ValueTask<data.@this> Of(global::app.goal.step.action.@this action)
    {
        await Load();
        var node = new global::app.type.item.setting.action.@this(action);
        return Class(node.Path) != null ? Instance(node.Path) : new data.@this(node.Path, node, context: _context);
    }

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
