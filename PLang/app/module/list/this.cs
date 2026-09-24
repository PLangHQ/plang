using System.Collections.Concurrent;
using System.Reflection;
using app.module;
using app.actor.context;
using app.error;

namespace app.module.list;

/// <summary>
/// Flat action registry. Owns discovery, registration, and resolution of all actions.
/// Built-in actions are discovered from the PLang assembly at construction.
/// External DLLs add actions via Discover(assembly, namespace).
/// </summary>
public sealed class @this : IAsyncDisposable
{
    // The collection owns MODULES — selection and lifecycle. Each module owns its own actions;
    // there is no module→action index here to keep in step with them.
    private readonly ConcurrentDictionary<string, global::app.module.@this> _modules = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>The App this registry serves — handed at construction, like every other collection
    /// App builds (<c>Type</c> takes its context the same way). Never assigned afterwards.</summary>
    public global::app.@this App { get; }

    /// <summary>
    /// The type-catalog's LLM view — PrimitiveNames / Types / Kinds, "what the type vocabulary
    /// looks like for the LLM." Built on demand via <c>Schema.Build()</c> (which reads
    /// <c>App.Type</c>).
    /// </summary>
    public global::app.type.list.view.@this Schema { get; }

    public @this(global::app.@this app)
    {
        App = app;
        Schema = new global::app.type.list.view.@this(this);
        Discover(typeof(@this).Assembly, "app.module.action");
    }

    /// <summary>
    /// Disposes every registered handler instance (IAsyncDisposable preferred,
    /// IDisposable fallback). Same projection as <see cref="All"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var module in _modules.Values) await module.DisposeAsync();
    }

    /// <summary>
    /// Discovers [Action]-attributed ICodeGenerated types in an assembly and registers them.
    /// External DLLs call this via module.add.
    /// </summary>
    public int Discover(Assembly assembly, string? baseNamespace = null)
    {
        baseNamespace ??= "app.module.action";
        int count = 0;

        var actionTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ActionAttribute>() != null
                      && typeof(ICodeGenerated).IsAssignableFrom(t)
                      && !t.IsAbstract);

        foreach (var type in actionTypes)
        {
            if (type.Namespace == null || !type.Namespace.StartsWith(baseNamespace + "."))
                continue;

            var module = type.Namespace[(baseNamespace.Length + 1)..];
            var attr = type.GetCustomAttribute<ActionAttribute>()!;
            var actionName = attr.Name ?? type.Name.ToLowerInvariant();

            RegisterType(module, actionName, type);
            count++;
        }

        // A code.load'd assembly may bring its own closed sets (choice<T> params) — register them
        // once its actions are in. At boot App isn't attached yet; the app ctor registers the PLang
        // assembly's choices explicitly, so this only fires for a runtime-loaded assembly.
        App?.Type.Choice.Register(assembly);

        return count;
    }

    /// <summary>
    /// Registers an action type for per-call instantiation (stateless, normal path).
    /// </summary>
    public void RegisterType(string module, string actionName, Type type)
        => Element(module).Add(actionName, type, null);

    // Get-or-create the module, then the MODULE takes the action. Registration never reaches two
    // levels deep into someone else's contents.
    private global::app.module.@this Element(string name)
        => _modules.GetOrAdd(name, n => new global::app.module.@this(n, this));


    /// <summary>
    /// Registers a shared action instance (stateful — external DLLs, test overrides).
    /// Instance takes priority over type during resolution.
    /// </summary>
    public void Register(string module, string actionName, IAction instance)
        => Element(module).Add(actionName, null, instance);

    // --- Queries ---

    /// <summary>Does this module exist? Whether it HAS an action is the module's own question:
    /// <c>list[module][action] != null</c>.</summary>
    public bool Contains(string module)
        => _modules.ContainsKey(module);

    public IEnumerable<string> Names
        => _modules.Keys;

    // --- Selection + enumeration: the concept's element surface ---

    /// <summary>Select a module by name. Throws on miss (names are authored). There is no second
    /// cache to invalidate — the module IS the entry.</summary>
    public global::app.module.@this this[string name]
        => _modules.TryGetValue(name, out var module)
            ? module
            : throw new KeyNotFoundException($"No module named '{name}'.");

    /// <summary>The modules as the NATIVE plang list — filterable by the list module,
    /// renderable by templates. A fresh, cheap wrapper per ask over the same cached elements.</summary>
    public global::app.type.item.list.@this list
        => new(Names.Select(n => (object?)this[n]).ToList());

    /// <summary>The names a module answers to — asked OF the module, tolerating an unknown one so
    /// callers probing an arbitrary name need no pre-check.</summary>
    public IEnumerable<string> GetActions(string module)
        => _modules.TryGetValue(module, out var m) ? m.ActionNames : Enumerable.Empty<string>();

    public Type? GetActionType(string module, string actionName)
        => _modules.TryGetValue(module, out var m) ? m.Handler(actionName) : null;

    public int Count => _modules.Values.Sum(m => m.Count);

    /// <summary>
    /// All registered instances (for disposal on app shutdown).
    /// Type-registered actions are per-call — no disposal tracking needed.
    /// </summary>
    public IEnumerable<IAction> All
        => _modules.Values.SelectMany(m => m.Instances);

    /// <summary>
    /// Removes all actions for a module. Returns true if the module existed.
    /// </summary>
    public bool Remove(string module)
    {
        if (!_modules.TryRemove(module, out var removed)) return false;
        removed.Clear();   // authoritative: anyone still holding the element finds it empty
        return true;
    }

    public void Clear()
    {
        foreach (var module in _modules.Values) module.Clear();
        _modules.Clear();
    }

    /// <summary>Where per-action LLM teaching markdown lives — <c>/system/modules</c>, resolved
    /// through <c>path.Resolve</c> so every downstream read passes <c>AuthGate</c>. FilePath's
    /// ValidatePath redirects <c>/system/*</c> to <c>&lt;OsDirectory&gt;/system/*</c> when the path
    /// isn't present under the App root. Null only before the System context exists.</summary>
    public global::app.type.item.path.@this? Teaching
        => App?.System?.Context == null ? null
            : global::app.type.item.path.@this.Resolve("/system/modules", App.System.Context);
}

/// <summary>
/// Single registry entry — either a Type (per-call instantiation) or a shared Instance.
/// </summary>
public record ActionEntry(Type? Type, IAction? Instance)
{
    public ICodeGenerated? Create(global::app.actor.context.@this context)
    {
        // Shared mock instances (test-only) ignore per-call context — they set it via Attach.
        if (Instance is ICodeGenerated cg)
            return cg;

        // Generated actions are born WITH context — their primary ctor takes it.
        if (Type != null && typeof(ICodeGenerated).IsAssignableFrom(Type))
            return (ICodeGenerated)Activator.CreateInstance(Type, context)!;

        return null;
    }
}
