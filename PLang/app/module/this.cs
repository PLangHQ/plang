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
    global::app.type.item.IMatch<@this>, global::app.type.item.ILoad<@this>
{
    // what a program adds to the module — kept as long as the module is registered
    private readonly global::app.type.item.kept.list.@this _kept = new();
    internal override global::app.type.item.kept.list.@this Kept => _kept;

    /// <summary>A module is registered from its actions' classes, never made from a value.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this module) return module;
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — " +
            "a module is registered from its actions, never made from a value.", "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>A key names this module by its name; case is not the program's to get right.</summary>
    public System.Threading.Tasks.ValueTask<@this?> Match(string key)
        => System.Threading.Tasks.ValueTask.FromResult(string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase) ? this : null);

    /// <summary>A structure — written through the reflection kind, its [Out]/[Debug] members.</summary>
    public override bool IsLeaf => false;

    // The app's module, which holds every module; null on the app's module itself.
    private readonly @this? _root;
    // Held by the app's module only: the app, the modules, and the lock a registration takes.
    private readonly global::app.@this? _app;
    private readonly global::app.type.item.list.@this<@this>? _modules;
    private readonly object? _registering;

    /// <summary>The module name — "file", "variable", "list"; empty for the app's module.</summary>
    [Debug, Out, LlmBuilder]
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

    /// <summary>The code's module — the bottom, holding every module the code ships, born with
    /// <paramref name="app"/>, the app the code is made for, which answers for them.</summary>
    internal @this(global::app.@this app) : base(app.type.list[typeof(@this)])
    {
        Name = "";
        _app = app;
        _modules = new global::app.type.item.list.@this<@this>(app.type.list[typeof(global::app.type.item.list.@this<@this>)]);
        _registering = new object();
        Discover(typeof(@this).Assembly);
    }

    /// <summary>An app's module — a layer of its own over the registry <paramref name="under"/>, which it holds whole as
    /// its list's first row and reads through: a module it reaches is that registry's own object, a module it adds is
    /// its layer's alone, and what the registry it is over loads later is this one's too.</summary>
    internal @this(global::app.@this app, @this under) : base(app.type.list[typeof(@this)])
    {
        Name = "";
        _app = app;
        _modules = new global::app.type.item.list.@this<@this>(app.type.list[typeof(global::app.type.item.list.@this<@this>)]);
        _modules.Add(under._modules!);
        _registering = new object();
    }

    internal @this(string name, @this root) : base(root.Type)
    {
        Name = name;
        _root = root;
    }

    /// <summary>Whether this registry, a layer, holds modules of its own — a module this layer made.</summary>
    internal bool HasOwn => System.Threading.Volatile.Read(ref _own) > 0;

    // How many modules this registry made and holds: changed only by its own two doors, Of and Drop.
    private int _own;

    // The app's module: this one, or the registry holding it.
    internal @this Root => _root ?? this;

    /// <summary>The App this module belongs to — reached through the app's module.</summary>
    internal global::app.@this App => Root._app!;

    /// <summary>Every module the app has loaded — a plain list, the same for every module. Navigated
    /// (<c>%!app.module.list%</c>), never written with a module: every module reaches it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [global::app.LlmBuilder]
    public global::app.type.item.list.@this<@this> list => Root._modules!;

    /// <summary>The module <paramref name="name"/> names, by its own <see cref="Match"/>; NotFound when none does. The
    /// answer is born with the asker's <paramref name="context"/>.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this<@this>> Get(string name, global::app.actor.context.@this context)
    {
        foreach (var module in list.Items())
            if (await module.Match(name) is { } match) return context.Ok<@this>(match);
        return global::app.data.@this<@this>.FromError(new global::app.error.@this($"no module '{name}'", "NotFound", 404), context);
    }

    /// <summary>The module a <c>.pr</c> row names — for the readers, which read a synchronous pass; null when the
    /// name isn't one of the app's modules.</summary>
    internal @this? Named(string name)
        => list.Items().FirstOrDefault(m => string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>Registers an action class for per-call instantiation in the module named <paramref name="module"/>
    /// — the module is made the first time an action names it.</summary>
    public void Register(string module, string actionName, System.Type type)
    {
        var owner = Of(module);
        // the same action again is the same answer, whichever registry holds its module
        if (owner[actionName]?.Class == type) return;
        owner.Root.Register(owner, actionName, type, Root);
    }

    // An action into one of this registry's modules, asked by `asker`'s registry.
    private void Register(@this module, string action, System.Type type, @this asker)
    {
        // the fact itself, not a flag: a change to the asker's own rows it holds, one to a row below it is its origin's
        // to answer (decision 40)
        if (!ReferenceEquals(this, asker)) asker.App.origin.Register(module, action, type);
        else module.Add(action, type);
    }

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
            if (Named(name) is { } module) return module;
            var born = new @this(name, Root);
            list.Add(born);
            System.Threading.Interlocked.Increment(ref Root._own);
            return born;
        }
    }

    /// <summary>Takes this module out of the app — emptied too, so anyone still holding it finds no actions.</summary>
    public System.Threading.Tasks.ValueTask<bool> Remove(actor.context.@this context)
        => Root.Remove(this, context.App.module, context);

    // One of this registry's modules taken out, asked by `asker`'s registry.
    private System.Threading.Tasks.ValueTask<bool> Remove(@this module, @this asker, actor.context.@this context)
        // the fact itself, not a flag: a change to the asker's own rows it holds, one to a row below it is its origin's
        // to answer (decision 40)
        => ReferenceEquals(this, asker) ? Drop(module, context) : asker.App.origin.Remove(module, context);

    /// <summary>Takes <paramref name="module"/>, one this registry holds, out of it, for every app over it too —
    /// emptied, so anyone still holding it finds no actions.</summary>
    internal async System.Threading.Tasks.ValueTask<bool> Drop(@this module, actor.context.@this context)
    {
        if (!await list.Remove(module, context)) return false;
        System.Threading.Interlocked.Decrement(ref _own);
        module.Clear();
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
        var types = App.type.list;
        // The catalog action is born with its class's properties, reflected on first read.
        _action[actionName] =
            typeof(global::app.module.IClause).IsAssignableFrom(clr)
                ? new global::app.goal.step.action.clause.@this(types)
                    { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) }
            : typeof(global::app.module.ILoop).IsAssignableFrom(clr)
                ? new global::app.goal.step.action.loop.@this(types)
                    { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) }
            : typeof(global::app.module.IKeep).IsAssignableFrom(clr)
                ? new global::app.goal.step.action.keep.@this(types)
                    { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) }
            : new global::app.goal.step.action.@this(types)
                { Module = this, Name = actionName, Cacheable = cacheable, Class = clr, Property = new(this, actionName) };
    }

    /// <summary>The module's actions as the NATIVE plang list — step actions and clauses alike (the type
    /// IS the role). Filterable by the list module, renderable by templates.</summary>
    [LlmBuilder]
    public global::app.type.item.list.@this Action
        => new(_action.Values.Select(a => (object?)a).ToList(), App.type.list["list"]);

    /// <summary>Select one catalog element by action name — a step action or a clause; the type answers
    /// the role. Null when the name isn't in this module.</summary>
    public global::app.goal.step.action.@this? this[string actionName]
        => _action.TryGetValue(actionName, out var action) ? action : null;

    /// <summary>One step by dot: the module's own members first (<c>.name</c>, <c>.list</c>, <c>.action</c>, …), then
    /// one of its children by name — <c>%!module.file.read%</c> is the catalog action a program binds events on. An
    /// item's inner members are not the module's: <c>%!module.variable%</c> is the variable module.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => !Declares(key) && Child(key) is { } child
            ? new global::app.data.@this(key, child, parent: parent)
            : await base.Get(parent, key);

    /// <summary>Brackets pick a child by name — <c>%!module["list"]%</c> is the list module, whose name the app's
    /// module's own <c>.list</c> takes.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key, bool isIndex)
        => !isIndex ? await Get(parent, key)
            : Child(key) is { } child ? new global::app.data.@this(key, child, parent: parent) : parent.Context.NotFound(key);

    // One child by name: the app's module holds modules, a module its actions.
    private global::app.type.item.@this? Child(string key) => _root == null ? Named(key) : this[key];

    // Whether key is one of the module class's own members.
    private bool Declares(string key)
        => GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.DeclaredOnly) != null;

    /// <summary>This module's settings — <c>%!llm.setting%</c>: its own class, or the options its actions take.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Setting(global::app.data.@this parent)
        // its own class, as the asker's settings build it; else the options its actions take, read and written through them
        => _root == null ? await base.Setting(parent)
            : await parent.Context.Setting.Get(Address, () => new global::app.type.item.setting.module.@this(Name,
                parent.Context.App.type.list[typeof(global::app.type.item.setting.module.@this)]));

    /// <summary>Where this module's settings are kept — its name, then <c>.setting</c> (<c>llm.setting</c>).</summary>
    internal string Address => $"{Name}.setting";

    /// <summary>Select the catalog element that holds <paramref name="clr"/> — the module and the name registration
    /// gave the class; the app's module asks each of its modules. Null when no module holds it.</summary>
    internal global::app.goal.step.action.@this? this[System.Type clr]
        => _root == null
            ? list.Items().Select(module => module[clr]).FirstOrDefault(action => action != null)
            : _action.Values.FirstOrDefault(action => action.Class == clr);

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

    /// <summary>The module's docs folder — os/system/modules/{Name}; the app's module, which holds every
    /// module and has no name, is the modules folder itself. Its actions reach their own doc files through it.</summary>
    internal global::app.type.item.path.@this Folder => _root is null ? Teaching : Teaching.Combine(Name);

    // A lazy file handle: born unread, content materializes at the Value door (AuthGate'd path
    // verbs), and an absent file is falsy (existence truthiness), so `{% if module.Description %}`
    // guards presence without reading.
    private global::app.type.item.file.@this? _description;

    /// <summary>The module's description — module.description.md.</summary>
    [LlmBuilder]
    public global::app.type.item.file.@this Description => _description ??= new(Folder.Combine("module.description.md"), App.actor.list.System.Context!);

    private global::app.type.item.file.@this? _notes;

    /// <summary>The module's notes — module.notes.md; falsy when the module has none.</summary>
    [LlmBuilder]
    public global::app.type.item.file.@this Notes => _notes ??= new(Folder.Combine("module.notes.md"), App.actor.list.System.Context!);

    private global::app.type.item.file.@this? _guide;

    /// <summary>The module's guide — module.guide.md: prose for a learner, shown on the module's page and never read by
    /// the builder; falsy when the module has none.</summary>
    [LlmBuilder]
    public global::app.type.item.file.@this Guide => _guide ??= new(Folder.Combine("module.guide.md"), App.actor.list.System.Context!);
}
