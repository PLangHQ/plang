using System.Reflection;
using app.module;
using app.actor.context;
using app.error;

namespace app.module.list;

/// <summary>
/// The app's modules — a list of them, each owning its actions. Its own work: discovering
/// [Action]-attributed classes in an assembly and registering each with its module (built-ins from the
/// PLang assembly at construction; external DLLs through <c>module.add</c>), and disposing the shared
/// instances the modules hold. One module is picked by name through the type:
/// <c>app.module.Get("file")</c>.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.module.@this>, IAsyncDisposable
{
    private bool _disposed;
    private readonly object _registering = new();

    /// <summary>The App this list serves — handed at construction. Never assigned afterwards.</summary>
    public global::app.@this App { get; }

    public @this(global::app.@this app) : base(new List<object?>())
    {
        App = app;
        Discover(typeof(@this).Assembly, "app.module.action");
    }

    /// <summary>
    /// Disposes every registered handler instance the modules hold (IAsyncDisposable preferred,
    /// IDisposable fallback).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var module in Items()) await module.DisposeAsync();
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

        return count;
    }

    /// <summary>
    /// Registers an action type for per-call instantiation (stateless, normal path).
    /// </summary>
    public void RegisterType(string module, string actionName, Type type)
        => Element(module).Add(actionName, type, null);

    /// <summary>
    /// Registers a shared action instance (stateful — external DLLs, test overrides).
    /// Instance takes priority over type during resolution.
    /// </summary>
    public void Register(string module, string actionName, IAction instance)
        => Element(module).Add(actionName, null, instance);

    // Get-or-create the module, then the MODULE takes the action. Registration never reaches two
    // levels deep into someone else's contents.
    private global::app.module.@this Element(string name)
    {
        lock (_registering)
        {
            if (Items().FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) is { } held)
                return held;
            var born = new global::app.module.@this(name, this);
            Add(born);
            return born;
        }
    }

    /// <summary>
    /// The module a <c>.pr</c> row names — for the readers only, which read a synchronous pass and birth
    /// a module in every action row (as the type list's lookup births a type in every value slot).
    /// Everything else picks through the type: <c>await app.module.Get(name)</c>. A name that isn't one
    /// of this app's modules is the row's format error; the goal load turns it into a result.
    /// </summary>
    internal global::app.module.@this this[string name]
        => Items().FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new System.Text.Json.JsonException($"module '{name}' isn't one of this app's modules.");

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
