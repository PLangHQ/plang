using System.Reflection;

namespace app.module;

/// <summary>
/// One module — "file", "variable", "list": the actions of one kind. An item that is never authored
/// or created from a value (<see cref="Create"/> declines). The app's module (<c>%!app.module%</c>) is an
/// empty one carrying <see cref="list"/> — every loaded module, a plain <c>list&lt;module&gt;</c>, which every
/// module reaches as its own <c>.list</c>; one is picked by name through it (<c>app.module.Get("file")</c>,
/// <c>%!app.module.file%</c>). Navigated by reflection, read by templates through its own doors.
/// </summary>
[global::app.Attributes.PlangType("module")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.IMatch<@this>, global::app.type.item.ICurrent<@this>, global::app.type.item.ILoad<@this>
{
    /// <summary>A module is registered from its actions' classes, never made from a value.</summary>
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        if (raw is @this module) return module;
        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a module is registered from its actions, never made from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A key names this module by its name; case is not the program's to get right.</summary>
    public System.Threading.Tasks.ValueTask<@this?> Match(string key)
        => System.Threading.Tasks.ValueTask.FromResult(string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase) ? this : null);

    /// <summary>A structure — written through the reflection kind, its [Out]/[Debug] members.</summary>
    public override bool IsLeaf => false;

    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
        => new global::app.type.item.kind.reflection.@this().Output(this, writer, mode, context);

    // The app's module, which holds every module; null on the app's module itself.
    private readonly @this? _root;
    // Held by the app's module only: the app, the modules, and the lock a registration takes.
    private readonly global::app.@this? _app;
    private readonly global::app.type.item.list.@this<@this>? _modules;
    private readonly object? _registering;

    /// <summary>The module name — "file", "variable", "list"; empty for the app's module.</summary>
    [Debug, Out]
    public string Name { get; }

    /// <summary>The module IS its name in text — so a site composing the qualified form
    /// (<c>$"{action.Module}.{action.Name}"</c>, <c>{{ a.Module }}.{{ a.Name }}</c>) reads
    /// naturally without reaching for <c>.Name</c>.</summary>
    public override string ToString() => Name;

    // The module's actions — ITS OWN storage, filled as each one registers. One map, because the
    // ROLE is decided once, here: an action whose handler is a clause (IClause — on.error, on.cache,
    // on.timeout) is minted as the clause subtype at registration, so "the type IS the role" needs no
    // second home and no flag; a program action is made by its catalog element (Program).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.goal.step.action.@this> _action
        = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>The app's module — empty, holding every module the app loads.</summary>
    internal @this(global::app.@this app)
    {
        Name = "";
        _app = app;
        _modules = new global::app.type.item.list.@this<@this>();
        _registering = new object();
        Discover(typeof(@this).Assembly);
    }

    internal @this(string name, @this root)
    {
        Name = name;
        _root = root;
    }

    // The app's module: this one, or the one holding it.
    private @this Root => _root ?? this;

    /// <summary>The App this module belongs to — reached through the app's module.</summary>
    internal global::app.@this App => Root._app!;

    /// <summary>Every module the app has loaded — a plain list, the same for every module.</summary>
    public global::app.type.item.list.@this<@this> list => Root._modules!;

    /// <summary>The module <paramref name="name"/> names, by its own <see cref="Match"/>; NotFound when none does.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this<@this>> Get(string name)
    {
        foreach (var module in list.Items())
            if (await module.Match(name) is { } found) return global::app.data.@this<@this>.Ok(found);
        return global::app.data.@this<@this>.FromError(new global::app.error.Error($"no module '{name}'", "NotFound", 404));
    }

    /// <summary>The module a <c>.pr</c> row names — for the readers, which read a synchronous pass; null when the
    /// name isn't one of the app's modules.</summary>
    internal @this? Named(string name)
        => list.Items().FirstOrDefault(m => string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>Registers an action class for per-call instantiation in the module named <paramref name="module"/>
    /// — the module is made the first time an action names it.</summary>
    public void Register(string module, string actionName, System.Type type) => Of(module).Add(actionName, type);

    /// <summary>Registers every [Action] class under <paramref name="baseNamespace"/> in an assembly — the module is
    /// the namespace segment after it. The built-ins at birth; an external DLL through <c>module.add</c>.</summary>
    public int Discover(Assembly assembly, string? baseNamespace = null)
    {
        baseNamespace ??= "app.module";
        int count = 0;
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ICodeGenerated).IsAssignableFrom(type)) continue;
            if (type.GetCustomAttribute<ActionAttribute>() is not { } attr) continue;
            if (type.Namespace == null || !type.Namespace.StartsWith(baseNamespace + ".")) continue;
            Register(type.Namespace[(baseNamespace.Length + 1)..], attr.Name ?? type.Name.ToLowerInvariant(), type);
            count++;
        }
        return count;
    }

    // The module by its name, made and listed when it isn't yet — under the app's module's lock, so parallel
    // registrations reach one module.
    private @this Of(string name)
    {
        lock (Root._registering!)
        {
            if (Named(name) is { } held) return held;
            var born = new @this(name, Root);
            list.Add(born);
            return born;
        }
    }

    /// <summary>Takes this module out of the app — emptied too, so anyone still holding it finds no actions.</summary>
    public async System.Threading.Tasks.ValueTask<bool> Remove(actor.context.@this context)
    {
        if (!await list.Remove(this, context)) return false;
        Clear();
        return true;
    }

    /// <summary>Takes one action: its catalog action, born from its class as the subtype the class says it is (a
    /// clause when it is an <see cref="IClause"/>, a loop when it is an <see cref="ILoop"/>, a keep when it is an
    /// <see cref="IKeep"/>), holding the class. The module is the only thing that ever adds to its own contents.</summary>
    internal void Add(string actionName, System.Type clr)
    {
        // The catalog action carries the [Action] cache flag so the teaching template can tag
        // [no-cache] — read off the attribute, its single source, not defaulted.
        var cacheable = clr.GetCustomAttribute<global::app.module.ActionAttribute>()?.Cacheable ?? true;
        // The catalog action is born with its class's properties, reflected on first read.
        _action[actionName] =
            typeof(global::app.module.IClause).IsAssignableFrom(clr)
                ? new global::app.goal.step.action.clause.@this
                    { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) }
            : typeof(global::app.module.ILoop).IsAssignableFrom(clr)
                ? new global::app.goal.step.action.loop.@this
                    { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) }
            : typeof(global::app.module.IKeep).IsAssignableFrom(clr)
                ? new global::app.goal.step.action.keep.@this
                    { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) }
            : new global::app.goal.step.action.@this
                { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) };
    }

    /// <summary>The module's actions as the NATIVE plang list — step actions and clauses alike (the type
    /// IS the role). Filterable by the list module, renderable by templates.</summary>
    public global::app.type.item.list.@this Action
        => new(_action.Values.Select(a => (object?)a).ToList());

    /// <summary>Select one catalog element by action name — a step action or a clause; the type answers
    /// the role. Null when the name isn't in this module.</summary>
    public global::app.goal.step.action.@this? this[string actionName]
        => _action.TryGetValue(actionName, out var action) ? action : null;

    /// <summary>One step down: the module's own members first (<c>.name</c>, <c>.action</c>, …), then one of its
    /// actions by name — <c>%!app.module.file.read%</c> is the catalog action a program binds events on.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        var member = await base.Get(parent, key);
        if (member.IsInitialized) return member;
        if (this[key] is { } action) return new global::app.data.@this(key, action, parent: parent);
        // the app's module holds no actions: a key is one of its modules (%!app.module.file%)
        return _root == null && Named(key) is { } module ? new global::app.data.@this(key, module, parent: parent) : member;
    }

    /// <summary>The class that runs one of this module's actions — its catalog action's.</summary>
    internal System.Type? Handler(string actionName) => this[actionName]?.Class;

    /// <summary>The runnable for one of this module's actions — its class, born with the run's context.</summary>
    internal ICodeGenerated? Create(string actionName, actor.context.@this context)
        => Handler(actionName) is { } clr && typeof(ICodeGenerated).IsAssignableFrom(clr)
            ? (ICodeGenerated)System.Activator.CreateInstance(clr, context)!
            : null;

    internal bool Contains(string actionName) => _action.ContainsKey(actionName);

    /// <summary>The names this module answers to — its own keys.</summary>
    internal IEnumerable<string> ActionNames => _action.Keys;

    internal int Count => _action.Count;

    /// <summary>Sheds every action this module owns. Unregistering a module must be authoritative
    /// even for code already holding the element (a revoked DLL's actions must stop resolving), and
    /// only the module can empty itself.</summary>
    internal void Clear() => _action.Clear();

    /// <summary>Where the modules' teaching markdown lives — <c>/system/modules</c>, resolved through
    /// <c>path.Resolve</c> so every read passes <c>AuthGate</c> (FilePath redirects <c>/system/*</c> to the
    /// OS directory when the app root has none).</summary>
    private global::app.type.item.path.@this Teaching
        => global::app.type.item.path.@this.Resolve("/system/modules", App.actor.list.System.Context);

    /// <summary>The module's docs folder — os/system/modules/{Name}. Its actions reach their own doc
    /// files through it.</summary>
    internal global::app.type.item.path.@this Folder => Teaching.Combine(Name);

    // A lazy file handle: born unread, content materializes at the Value door (AuthGate'd path
    // verbs), and an absent file is falsy (existence truthiness), so `{% if module.Description %}`
    // guards presence without reading.
    private global::app.type.item.file.@this? _description;

    /// <summary>The module's description — module.description.md.</summary>
    public global::app.type.item.file.@this Description => _description ??= new(Folder.Combine("module.description.md"), App.actor.list.System.Context!);

    private global::app.type.item.file.@this? _notes;

    /// <summary>The module's notes — module.notes.md; falsy when the module has none.</summary>
    public global::app.type.item.file.@this Notes => _notes ??= new(Folder.Combine("module.notes.md"), App.actor.list.System.Context!);
}
