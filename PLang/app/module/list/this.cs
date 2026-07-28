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

    /// <summary>
    /// Resolves a handler for a .pr action. Navigates the action for module/actionName.
    /// </summary>
    public (ICodeGenerated? Handler, IError? Error) GetCodeGenerated(
        global::app.goal.step.action.@this action, actor.context.@this context)
    {
        if (!action.Module.Contains(action.Name))
            return (null, ActionError.NotFound($"Action '{action.Module}.{action.Name}'"));

        var handler = action.Module.Create(action.Name, context);
        if (handler == null)
            return (null, new ActionError(
                $"Action '{action.Module}.{action.Name}' does not implement ICodeGenerated",
                "ActionError", 500));

        return (handler, null);
    }

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
        => new(Names.Select(n => (object?)this[n]).ToList(), App.System.Context);

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
            moduleName => _modules.TryGetValue(moduleName, out var m)
                ? m.ActionNames
                : Array.Empty<string>());

        foreach (var o in orphans)
        {
            var msg = $"Orphan teaching markdown: {o.Path} (no registered action '{o.Module}.{o.Stem}'). Rename the file, register the action, or delete the file.\n";
            await actor.Channel.WriteTextAsync(global::app.channel.list.@this.Output, msg, cancellationToken);
        }

        return orphans;
    }

    [System.Obsolete("Module discovery moves to app.module.action.list (list<module>) + a Fluid render — do not add new callers.")]
    public async Task<List<global::app.goal.step.action.@this>> Describe()
    {
        var result = new List<global::app.goal.step.action.@this>();
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
                result.Add(new global::app.goal.step.action.@this
                {
                    Module = this[ns],
                    Name = actionName,
                    Parameter = new global::app.goal.step.action.parameter.list.@this(parameters),
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
        if (!_modules.TryGetValue(ns, out var module))
            yield break;
        foreach (var t in module.HandlerTypes)
            yield return t;
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
