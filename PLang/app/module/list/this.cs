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
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ActionEntry>> _modules = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>Owning App, set by App constructor after Modules construction.</summary>
    public global::app.@this App { get; internal set; } = null!;

    /// <summary>
    /// The type-catalog's LLM view — PrimitiveNames / Types / Kinds, "what the type vocabulary
    /// looks like for the LLM." Built on demand via <c>Schema.Build()</c> (which reads
    /// <c>App.Type</c>). Example rendering moved out to <c>app.type.spec.render.@this</c>.
    /// </summary>
    public global::app.type.list.view.@this Schema { get; }

    public @this()
    {
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
        foreach (var entry in _modules.Values
                                      .SelectMany(a => a.Values)
                                      .Where(e => e.Instance != null))
        {
            var handler = entry.Instance!;
            if (handler is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (handler is IDisposable disposable)
                disposable.Dispose();
        }
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
    {
        var actions = _modules.GetOrAdd(module, _ => new ConcurrentDictionary<string, ActionEntry>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = new ActionEntry(type, null);
    }


    /// <summary>
    /// Registers a shared action instance (stateful — external DLLs, test overrides).
    /// Instance takes priority over type during resolution.
    /// </summary>
    public void Register(string module, string actionName, IAction instance)
    {
        var actions = _modules.GetOrAdd(module, _ => new ConcurrentDictionary<string, ActionEntry>(StringComparer.OrdinalIgnoreCase));
        actions[actionName] = new ActionEntry(null, instance);
    }

    /// <summary>
    /// Resolves a handler for a .pr action. Navigates the action for module/actionName.
    /// </summary>
    public (ICodeGenerated? Handler, IError? Error) GetCodeGenerated(
        global::app.goal.steps.step.actions.action.@this action, actor.context.@this context)
    {
        if (!_modules.TryGetValue(action.Module, out var actions) ||
            !actions.TryGetValue(action.ActionName, out var entry))
            return (null, ActionError.NotFound($"Action '{action.Module}.{action.ActionName}'"));

        var handler = entry.Create(context);
        if (handler == null)
            return (null, new ActionError(
                $"Action '{action.Module}.{action.ActionName}' does not implement ICodeGenerated",
                "ActionError", 500));

        return (handler, null);
    }

    // --- Queries ---

    public bool Contains(string module, string actionName)
        => _modules.TryGetValue(module, out var actions) && actions.ContainsKey(actionName);

    public bool Contains(string module)
        => _modules.ContainsKey(module);

    public IEnumerable<string> Names
        => _modules.Keys;

    // --- Selection + enumeration: the concept's element surface ---

    // Module elements cached — a fresh element mints on first selection and lives as long as
    // the registry entry (invalidated by the registry's own mutations: RegisterType/Register/
    // Remove/Clear each drop the element).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.module.@this> _elements
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Select a module element by name. Throws on miss (names are authored).</summary>
    public global::app.module.@this this[string name]
        => _modules.ContainsKey(name)
            ? _elements.GetOrAdd(name, n => new global::app.module.@this(n, this))
            : throw new KeyNotFoundException($"No module named '{name}'.");

    /// <summary>The modules as the NATIVE plang list — filterable by the list module,
    /// renderable by templates. A fresh, cheap wrapper per ask over the same cached elements.</summary>
    public global::app.type.item.list.@this list
        => new(Names.Select(n => (object?)this[n]).ToList(), App.System.Context);

    private global::app.module.action.@this? _action;

    /// <summary>The flat action collection — every module's actions + modifiers, enumerated at
    /// <c>.list</c> (<c>%!app.module.action.list%</c>). Per-module selection stays on the module
    /// collection (<c>this[name]</c>); this is the cross-module union the builder catalog walks.</summary>
    public global::app.module.action.@this action => _action ??= new(this);

    public IEnumerable<string> GetActions(string module)
        => _modules.TryGetValue(module, out var actions) ? actions.Keys : Enumerable.Empty<string>();

    public Type? GetActionType(string module, string actionName)
    {
        if (!_modules.TryGetValue(module, out var actions) ||
            !actions.TryGetValue(actionName, out var entry))
            return null;

        return entry.Type ?? entry.Instance?.GetType();
    }

    /// <summary>
    /// Returns whether an action is cacheable (from its [Action] attribute).
    /// Defaults to true if the action/attribute isn't found.
    /// </summary>
    public bool IsCacheable(string module, string actionName)
    {
        var type = GetActionType(module, actionName);
        if (type == null) return true;
        var attr = type.GetCustomAttribute<ActionAttribute>();
        return attr?.Cacheable ?? true;
    }

    public int Count => _modules.Values.Sum(a => a.Count);

    /// <summary>
    /// All registered instances (for disposal on app shutdown).
    /// Type-registered actions are per-call — no disposal tracking needed.
    /// </summary>
    public IEnumerable<IAction> All
        => _modules.Values.SelectMany(a => a.Values)
            .Where(e => e.Instance != null)
            .Select(e => e.Instance!);

    /// <summary>
    /// Removes all actions for a module. Returns true if the module existed.
    /// </summary>
    public bool Remove(string module)
        => _modules.TryRemove(module, out _);

    public void Clear() => _modules.Clear();

    /// <summary>
    /// Describes all registered actions with parameter metadata for the LLM builder prompt.
    /// global::app.module.list.@this owns this because it knows its own types.
    /// </summary>
    /// <summary>
    /// Returns the inventory of channel names visible to the given actor at build time
    /// (registered on actor.Channel). The builder catalog passes this to the LLM so it
    /// can pick a channel from real names — no `to <name>` pattern parsing.
    /// </summary>
    // Capability interfaces — their declared properties are wired by the source generator
    // from the execution context (Step, Channels, Event, Static, Context) and are NOT
    // user-supplied parameters. Describe() filters them so the catalog doesn't teach the
    // LLM to emit fields it must never emit.
    private static readonly System.Type[] CapabilityInterfaces =
    {
        typeof(IContext),
        typeof(IStep),
        typeof(IChannel),
        typeof(IEvent),
        typeof(IStatic),
    };

    /// <summary>
    /// Filesystem root for per-action LLM teaching markdown.
    /// Defaults to <c>{App.OsDirectory}/system/modules</c>; tests stage fixtures
    /// in a temp folder and assign this directly. Null disables markdown teaching
    /// (catalog still assembles — fields just stay null/empty).
    /// </summary>
    public string? MarkdownTeachingRoot { get; set; }

    /// <summary>
    /// Resolves the markdown root: explicit override wins, else derives from
    /// <c>App.OsDirectory</c>. Returns null when neither is available. The
    /// string is routed through <c>path.@this.Resolve</c> (System actor's
    /// Context) so every downstream read goes through <c>AuthGate</c>, even
    /// when the override points outside the app root.
    /// </summary>
    public global::app.type.item.path.@this? ResolveMarkdownTeachingRoot()
    {
        if (App?.System?.Context == null) return null;
        if (!string.IsNullOrEmpty(MarkdownTeachingRoot))
            return global::app.type.item.path.@this.Resolve(MarkdownTeachingRoot!, App.System.Context);
        // FilePath's ValidatePath redirects /system/* to <OsDirectory>/system/*
        // when the path isn't present under the App root.
        return global::app.type.item.path.@this.Resolve("/system/modules", App.System.Context);
    }

    /// <summary>
    /// Scans <see cref="ResolveMarkdownTeachingRoot"/> for orphan teaching files
    /// (stem is not <c>module</c> and not a registered action in its module folder).
    /// Writes one line per orphan to the supplied actor's <c>Output</c> channel —
    /// CLAUDE.md "No Console.* writes in production C#" applies, and architect's
    /// coder plan pins the channel: <c>WriteTextAsync(Output, …)</c>. Returns the
    /// orphans seen (handy for tests / instrumentation); throws nothing — orphans
    /// must never block a build.
    /// </summary>
    public async Task<IReadOnlyList<MarkdownTeaching.Orphan>> WarnOrphansAsync(
        global::app.actor.@this actor,
        CancellationToken cancellationToken = default)
    {
        var root = ResolveMarkdownTeachingRoot();
        var orphans = await MarkdownTeaching.ScanOrphans(root,
            moduleName => _modules.TryGetValue(moduleName, out var actions)
                ? actions.Keys
                : Array.Empty<string>());

        foreach (var o in orphans)
        {
            var msg = $"Orphan teaching markdown: {o.Path} (no registered action '{o.Module}.{o.Stem}'). Rename the file, register the action, or delete the file.\n";
            await actor.Channel.WriteTextAsync(global::app.channel.list.@this.Output, msg, cancellationToken);
        }

        return orphans;
    }

    [System.Obsolete("Module discovery moves to app.module.action.list (list<module>) + a Fluid render — do not add new callers.")]
    public async Task<List<global::app.goal.steps.step.actions.action.@this>> Describe()
    {
        var result = new List<global::app.goal.steps.step.actions.action.@this>();
        var nCtx = new NullabilityInfoContext();
        // Cache module descriptions by namespace — populated on first encounter per namespace

        foreach (var ns in Names)
        {
            foreach (var actionName in GetActions(ns))
            {
                var parameterType = GetActionType(ns, actionName);
                if (parameterType == null) continue;

                // Collect the property names contributed by any capability interfaces this
                // action implements. They'll be filtered out of the exposed catalog below.
                var capabilityProps = new HashSet<string>(
                    CapabilityInterfaces
                        .Where(iface => iface.IsAssignableFrom(parameterType))
                        .SelectMany(iface => iface.GetProperties().Select(p => p.Name)),
                    StringComparer.OrdinalIgnoreCase);

                var parameters = new List<data.@this>();

                foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.Name == "EqualityContract") continue;
                    if (capabilityProps.Contains(prop.Name)) continue;
                    if (prop.GetCustomAttribute<CodeAttribute>() != null) continue;

                    var typeName = ((App?.Type?.GetTypeName(prop.PropertyType) ?? global::app.type.list.@this.GetTypeNameStatic(prop.PropertyType)));

                    bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null;
                    if (!isNullable && !prop.PropertyType.IsValueType)
                        isNullable = nCtx.Create(prop).WriteState == NullabilityState.Nullable;
                    if (isNullable && !typeName.EndsWith("?"))
                        typeName += "?";

                    // Enum valid-values (operator, httpmethod, trigger, ...) are NOT inlined
                    // on each parameter any more — they're declared once in Type Information
                    // so repeating them here would just bloat the prompt. The type name alone
                    // (e.g. "operator") points the LLM to the Type Information entry.

                    var hasVar = IsVariableNameSlot(prop.PropertyType);
                    var defaultAttr = prop.GetCustomAttribute<DefaultAttribute>();

                    // Variable slots advertise as "%var%" — the marker alone tells the LLM
                    // this parameter takes a variable reference. Don't append a type token:
                    // `Variable` only constrains the slot to *name* a variable; what the
                    // variable resolves to at runtime is unconstrained (list, dict, bool,
                    // object — anything). A trailing "string" was a lie that produced
                    // spurious ambiguousMapping warnings when scope held a non-string.
                    var desc = hasVar ? "%var%" : typeName;
                    if (defaultAttr != null)
                        desc += $" = {FormatDefault(defaultAttr.Value)}";

                    parameters.Add(new data.@this(prop.Name, desc, context: App.System.Context));
                }

                // IChannel actions: source-gen reads action.Parameters["channel"] to resolve
                // the Channel slot. Surface that parameter to the LLM so it can emit a name
                // from the actor's channel inventory.
                if (typeof(IChannel).IsAssignableFrom(parameterType))
                    parameters.Add(new data.@this("channel", "string?", context: App.System.Context));

                bool cacheable = true;
                var actionAttr = parameterType.GetCustomAttribute<ActionAttribute>();
                if (actionAttr != null)
                    cacheable = actionAttr.Cacheable;


                var returnType = DescribeReturnType(parameterType);
                var returnTypeName = DescribeReturnTypeName(parameterType);

                // Teaching prose (Description / Notes / Examples) is no longer assembled here — it
                // rides as lazy `file` handles on the action/module elements (the class-zoom prose
                // doors over os/system/modules/{module}/{...}.md). Describe now carries only the
                // structural facts the param-desc parity still compares (params, return, cacheable).
                result.Add(new global::app.goal.steps.step.actions.action.@this
                {
                    Module = ns,
                    ActionName = actionName,
                    Parameters = parameters,
                    Cacheable = cacheable,
                    ReturnType = returnType,
                    ReturnTypeName = returnTypeName,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Returns all registered action types within a given module namespace.
    /// Used to search for [ModuleDescription] on any type in the namespace.
    /// </summary>
    private IEnumerable<System.Type> GetAllTypesInNamespace(string ns)
    {
        if (!_modules.TryGetValue(ns, out var actions))
            yield break;
        foreach (var entry in actions.Values)
        {
            var t = entry.Type ?? entry.Instance?.GetType();
            if (t != null)
                yield return t;
        }
    }

    /// <summary>
    /// Reads the PLang name of T from <c>Run()</c>'s declared return type
    /// <c>Task&lt;Data&lt;T&gt;&gt;</c>. Bare <c>Task&lt;Data&gt;</c> renders as <c>data</c>
    /// — the polymorphic default (everything is a Data, value type unknown statically).
    /// <c>Task&lt;Data&lt;object&gt;&gt;</c> renders the same — same intent, redundant T.
    /// Real types surface their PLang name. Source of truth = the method signature.
    /// </summary>
    private string? DescribeReturnTypeName(System.Type actionType)
    {
        var runMethod = actionType.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
        if (runMethod == null) return null;

        var returnType = runMethod.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            returnType = returnType.GetGenericArguments()[0];

        // Bare Data → "data" (polymorphic by default). An action that genuinely
        // never produces a value still returns *some* Data — empty Properties,
        // null Value — so "data" is honest. Saves declaring Data<object> everywhere.
        if (returnType == typeof(data.@this)) return "data";

        // Data<T>
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(data.@this<>))
        {
            var t = returnType.GetGenericArguments()[0];
            if (t == typeof(object)) return "data";
            return (App?.Type?.GetTypeName(t) ?? global::app.type.list.@this.GetTypeNameStatic(t));
        }

        // Something else — not a Data variant; surface nothing.
        return null;
    }

    private List<data.@this>? DescribeReturnType(System.Type actionType)
    {
        var runMethod = actionType.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
        if (runMethod == null) return null;

        var returnType = runMethod.ReturnType;

        // Unwrap Task<T> → T
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            returnType = returnType.GetGenericArguments()[0];

        // Plain Data — no extra properties to describe
        if (returnType == typeof(data.@this)) return null;

        // Must be a Data subclass
        if (!typeof(data.@this).IsAssignableFrom(returnType)) return null;

        // Collect public properties that are NOT on the base Data class
        var baseProps = typeof(data.@this).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var properties = new List<data.@this>();
        foreach (var prop in returnType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (baseProps.Contains(prop.Name)) continue;
            var typeName = ((App?.Type?.GetTypeName(prop.PropertyType) ?? global::app.type.list.@this.GetTypeNameStatic(prop.PropertyType)));
            properties.Add(new data.@this(prop.Name, typeName, context: App.System.Context));
        }

        return properties.Count > 0 ? properties : null;
    }

    /// <summary>
    /// Returns default values for an action's parameters that aren't already provided.
    /// Checks IConfigure&lt;TConfig&gt; first, falls back to [Default] attributes.
    /// </summary>
    public List<data.@this>? GetDefaults(string module, string actionName, HashSet<string> excludeParams)
    {
        var actionType = GetActionType(module, actionName);
        if (actionType == null) return null;

        // Defaults come from [Default] attributes on the action params (the setting cascade's floor).
        var attrDefaults = new List<data.@this>();
        foreach (var prop in actionType.GetProperties())
        {
            if (excludeParams.Contains(prop.Name)) continue;
            var attrs = prop.GetCustomAttributes(typeof(DefaultAttribute), false);
            if (attrs.Length == 0) continue;
            attrDefaults.Add(new data.@this(prop.Name.ToLowerInvariant(),
                ((DefaultAttribute)attrs[0]).Value, context: App.System.Context));
        }
        return attrDefaults.Count > 0 ? attrDefaults : null;
    }

    private static string FormatDefault(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => value.ToString() ?? "null"
    };

    /// <summary>
    /// True when <paramref name="propType"/> is <c>Data&lt;variable&gt;</c> (or its
    /// nullable wrap). The property type is the carrier of "this slot names a variable" —
    /// the catalog builder uses this to mark <c>%var%</c>-shape parameters in the LLM prompt.
    /// </summary>
    private static bool IsVariableNameSlot(Type propType)
    {
        var underlying = Nullable.GetUnderlyingType(propType) ?? propType;
        if (!underlying.IsGenericType) return false;
        if (underlying.GetGenericTypeDefinition() != typeof(data.@this<>)) return false;
        var inner = underlying.GetGenericArguments()[0];
        return inner == typeof(app.variable.@this);
    }
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
