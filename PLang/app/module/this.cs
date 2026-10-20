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
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
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
    // Held by the app's module only: the app, and the modules — made when first asked, from the ones the code lists
    // (each module makes its actions when it is first asked for them); held by reference, the first made kept. A
    // registration locks the modules.
    private readonly global::app.@this? _app;
    private global::app.type.item.list.@this<@this>? _modules;

    /// <summary>The module name — "file", "variable", "list"; empty for the app's module.</summary>
    [Debug, Out, LlmBuilder]
    public string Name { get; }

    /// <summary>The module IS its name in text — so a site composing the qualified form
    /// (<c>$"{action.Module}.{action.Name}"</c>, <c>{{ a.Module }}.{{ a.Name }}</c>) reads
    /// naturally without reaching for <c>.Name</c>.</summary>
    public override string ToString() => Name;

    /// <summary>The app's module — empty, holding every module the app loads.</summary>
    internal @this(global::app.@this app) : base(app.type.list[typeof(@this)])
    {
        Name = "";
        _app = app;
    }

    // The app's modules: the built-ins as plang's code says them (read once for every app made from it), made the first
    // time any module is asked.
    private global::app.type.item.list.@this<@this> Modules
    {
        get
        {
            if (_modules is { } held) return held;
            var made = new global::app.type.item.list.@this<@this>(_app!.type.list[typeof(global::app.type.item.list.@this<@this>)]);
            foreach (var module in _app.type.list.Code.module) made.Add(new @this(module.Name, this, module.Action));
            return System.Threading.Interlocked.CompareExchange(ref _modules, made, null) ?? made;
        }
    }

    internal @this(string name, @this root, System.Collections.Generic.IReadOnlyList<global::app.assembly.@this.Action>? born = null)
        : base(root.Type)
    {
        Name = name;
        _root = root;
        _born = born ?? [];
    }

    // The actions the code gave this module at its birth — made into the module's actions on the first ask.
    private readonly System.Collections.Generic.IReadOnlyList<global::app.assembly.@this.Action> _born = [];

    // The module's actions, made on the first ask; held and compared by reference (a program binds events on an
    // action), so the first made is the one kept.
    private global::app.type.item.list.@this<global::app.goal.step.action.@this>? _action;

    /// <summary>The module's actions — step actions and clauses alike (the type IS the role: an action whose handler is
    /// a clause is made the clause subtype, so the role needs no flag). Made from the module's action classes when
    /// first asked. Filterable by the list module, renderable by templates.</summary>
    [LlmBuilder]
    public global::app.type.item.list.@this<global::app.goal.step.action.@this> Action
    {
        get
        {
            if (_action is { } held) return held;
            var made = new global::app.type.item.list.@this<global::app.goal.step.action.@this>(
                _born.Select(action => (global::app.type.item.@this)Made(action.Name, action.Class, action.Cacheable)),
                App.type.list[typeof(global::app.type.item.list.@this<global::app.goal.step.action.@this>)]);
            return System.Threading.Interlocked.CompareExchange(ref _action, made, null) ?? made;
        }
    }

    // The app's module: this one, or the one holding it.
    private @this Root => _root ?? this;

    /// <summary>The App this module belongs to — reached through the app's module.</summary>
    internal global::app.@this App => Root._app!;

    /// <summary>Every module the app has loaded — a plain list, the same for every module. Navigated
    /// (<c>%!app.module.list%</c>), never written with a module: every module reaches it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [global::app.LlmBuilder]
    public global::app.type.item.list.@this<@this> list => Root.Modules;

    /// <summary>Whether the app's modules are made — until they are, nothing is bound on any of them (binding names one,
    /// which makes them).</summary>
    internal bool HasList => Root._modules != null;

    /// <summary>The module <paramref name="name"/> names, by its own <see cref="Match"/>; NotFound when none does. The
    /// answer is born with the asker's <paramref name="context"/>.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this<@this>> Get(string name, global::app.actor.context.@this context)
    {
        foreach (var module in list.Items())
            if (await module.Match(name) is { } found) return context.Ok<@this>(found);
        return global::app.data.@this<@this>.FromError(new global::app.error.@this($"no module '{name}'", "NotFound", 404), context);
    }

    /// <summary>The module a <c>.pr</c> row names — for the readers, which read a synchronous pass; null when the
    /// name isn't one of the app's modules.</summary>
    internal @this? Named(string name)
    {
        // walked by position: an action asks it at each of its event levels, so the walk allocates nothing
        var modules = list;
        for (int at = 0, count = modules.CountRaw; at < count; at++)
            if (modules[at] is { } module && string.Equals(module.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return module;
        return null;
    }

    /// <summary>Registers an action class for per-call instantiation in the module named <paramref name="module"/>
    /// — the module is made the first time an action names it.</summary>
    public void Register(string module, string actionName, System.Type type)
        => Of(module).Add(actionName, type, type.GetCustomAttribute<global::app.module.ActionAttribute>()?.Cacheable ?? true);

    /// <summary>Registers every [Action] class under <paramref name="baseNamespace"/> in an assembly — the module is
    /// the namespace segment after it. The built-ins at birth; an external DLL through <c>module.add</c>.</summary>
    public int Discover(Assembly assembly, string? baseNamespace = null)
    {
        int count = 0;
        foreach (var module in new global::app.assembly.@this(assembly).Under(baseNamespace ?? "app.module"))
            foreach (var action in module.Action)
            {
                Of(module.Name).Add(action.Name, action.Class, action.Cacheable);
                count++;
            }
        return count;
    }

    // The module by its name, made and listed when it isn't yet — under the app's module's lock, so parallel
    // registrations reach one module.
    private @this Of(string name)
    {
        lock (Root.Modules)
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
    /// An action of the same name is replaced.
    internal void Add(string actionName, System.Type clr, bool cacheable)
    {
        var made = Made(actionName, clr, cacheable);
        lock (Root.Modules)
        {
            var index = Action.Items().Select((action, at) => (action, at))
                .FirstOrDefault(each => string.Equals(each.action.Name, actionName, System.StringComparison.OrdinalIgnoreCase));
            if (index.action != null) Action.RemoveAt(index.at);
            Action.Add(made);
        }
    }

    // The catalog action for one action class. It carries the [Action] cache flag (read off the attribute, its single
    // source) so the teaching template can tag [no-cache].
    private global::app.goal.step.action.@this Made(string actionName, System.Type clr, bool cacheable)
    {
        var types = App.type.list;
        // The catalog action is born with its class's properties, reflected on first read.
        return
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

    /// <summary>Select one of the module's actions by its name — a step action or a clause; the type answers
    /// the role. Null when the name isn't in this module.</summary>
    public global::app.goal.step.action.@this? this[string actionName]
    {
        get
        {
            // walked by position: every action a step runs is selected here, so the walk allocates nothing
            var action = Action;
            for (int at = 0, count = action.CountRaw; at < count; at++)
                if (string.Equals(action[at].Name, actionName, System.StringComparison.OrdinalIgnoreCase)) return action[at];
            return null;
        }
    }

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
        => _root != null ? await parent.Context.Setting.Of(this) : await base.Setting(parent);

    /// <summary>Select the catalog element that holds <paramref name="clr"/> — the module and the name registration
    /// gave the class; the app's module asks each of its modules. Null when no module holds it.</summary>
    internal global::app.goal.step.action.@this? this[System.Type clr]
        => _root == null
            ? list.Items().Select(module => module[clr]).FirstOrDefault(action => action != null)
            : Action.Items().FirstOrDefault(action => action.Class == clr);

    /// <summary>The class that runs one of this module's actions — its catalog action's.</summary>
    internal System.Type? Handler(string actionName) => this[actionName]?.Class;

    /// <summary>The runnable for one of this module's actions — its class, born with the run's context.</summary>
    internal ICodeGenerated? Create(string actionName, actor.context.@this context)
        => Handler(actionName) is { } clr && typeof(ICodeGenerated).IsAssignableFrom(clr)
            ? (ICodeGenerated)System.Activator.CreateInstance(clr, context)!
            : null;

    /// <summary>Sheds every action this module owns. Unregistering a module must be authoritative
    /// even for code already holding the element (a revoked DLL's actions must stop resolving), and
    /// only the module can empty itself.</summary>
    internal void Clear()
    {
        lock (Root.Modules)
            for (var at = Action.CountRaw - 1; at >= 0; at--) Action.RemoveAt(at);
    }

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
