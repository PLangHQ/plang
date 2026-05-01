using System.Collections.Concurrent;
using System.Reflection;
using App.modules;
using App.Actor.Context;
using App.Errors;

namespace App.Modules;

/// <summary>
/// Flat action registry. Owns discovery, registration, and resolution of all actions.
/// Built-in actions are discovered from the PLang assembly at construction.
/// External DLLs add actions via Discover(assembly, namespace).
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ActionEntry>> _modules = new(StringComparer.OrdinalIgnoreCase);

    public @this()
    {
        Discover(typeof(@this).Assembly, "App.modules");
    }

    /// <summary>
    /// Discovers [Action]-attributed ICodeGenerated types in an assembly and registers them.
    /// External DLLs call this via module.add.
    /// </summary>
    public int Discover(Assembly assembly, string? baseNamespace = null)
    {
        baseNamespace ??= "App.modules";
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
        Goals.Goal.Steps.Step.Actions.Action.@this action)
    {
        if (!_modules.TryGetValue(action.Module, out var actions) ||
            !actions.TryGetValue(action.ActionName, out var entry))
            return (null, ActionError.NotFound($"Action '{action.Module}.{action.ActionName}'"));

        var handler = entry.Create();
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
        var attr = type.GetCustomAttribute<modules.ActionAttribute>();
        return attr?.Cacheable ?? true;
    }

    /// <summary>
    /// Returns whether an action is a modifier (from its [Modifier] attribute).
    /// </summary>
    public bool IsModifier(string module, string actionName)
    {
        var type = GetActionType(module, actionName);
        return type?.GetCustomAttribute<modules.ModifierAttribute>() != null;
    }

    /// <summary>
    /// Returns the modifier nesting order (lower = outermost wrapper).
    /// int.MaxValue for non-modifier actions or when the attribute is missing.
    /// </summary>
    public int GetModifierOrder(string module, string actionName)
    {
        var type = GetActionType(module, actionName);
        var attr = type?.GetCustomAttribute<modules.ModifierAttribute>();
        return attr?.Order ?? int.MaxValue;
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
    /// AppModules owns this because it knows its own types.
    /// </summary>
    // Capability interfaces — their declared properties are wired by the source generator
    // from the execution context (Step, Channels, Event, Static, Context) and are NOT
    // user-supplied parameters. Describe() filters them so the catalog doesn't teach the
    // LLM to emit fields it must never emit.
    private static readonly System.Type[] CapabilityInterfaces =
    {
        typeof(modules.IContext),
        typeof(modules.IStep),
        typeof(modules.IChannel),
        typeof(modules.IEvent),
        typeof(modules.IStatic),
    };

    public Goals.Goal.Steps.Step.Actions.@this Describe()
    {
        var result = new Goals.Goal.Steps.Step.Actions.@this();
        var nCtx = new NullabilityInfoContext();
        // Cache module descriptions by namespace — populated on first encounter per namespace
        var moduleDescriptionCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

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

                var parameters = new List<Data.@this>();

                foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.Name == "EqualityContract") continue;
                    if (capabilityProps.Contains(prop.Name)) continue;
                    if (prop.GetCustomAttribute<modules.ProviderAttribute>() != null) continue;

                    var typeName = Utils.TypeMapping.GetTypeName(prop.PropertyType);

                    bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null;
                    if (!isNullable && !prop.PropertyType.IsValueType)
                        isNullable = nCtx.Create(prop).WriteState == NullabilityState.Nullable;
                    if (isNullable && !typeName.EndsWith("?"))
                        typeName += "?";

                    // Enum valid-values (operator, httpmethod, eventtype, ...) are NOT inlined
                    // on each parameter any more — they're declared once in Type Information
                    // so repeating them here would just bloat the prompt. The type name alone
                    // (e.g. "operator") points the LLM to the Type Information entry.

                    var hasVar = IsVariableNameSlot(prop.PropertyType);
                    var defaultAttr = prop.GetCustomAttribute<modules.DefaultAttribute>();

                    // Variable slots advertise as "%var% string" so the LLM emits
                    // a variable name (with or without %), not the literal type token.
                    var desc = hasVar ? "%var% string" : typeName;
                    if (defaultAttr != null)
                        desc += $" = {FormatDefault(defaultAttr.Value)}";

                    parameters.Add(new Data.@this(prop.Name, desc));
                }

                bool cacheable = true;
                var actionAttr = parameterType.GetCustomAttribute<modules.ActionAttribute>();
                if (actionAttr != null)
                    cacheable = actionAttr.Cacheable;

                // Prefer structured ExamplesForLlm() when the action class declares one.
                // The static method returns ExampleSpec[]; the renderer derives type tags
                // and nested-action JSON from reflection so authors only write meaning
                // (which action, which parameter, what value) — never raw type tags or
                // hand-built JSON. Falls back to [Example] attributes for not-yet-migrated
                // actions; the two coexist during transition.
                List<Data.@this> examples;
                var examplesForLlm = parameterType.GetMethod("ExamplesForLlm",
                    BindingFlags.Public | BindingFlags.Static, binder: null,
                    types: System.Type.EmptyTypes, modifiers: null);
                if (examplesForLlm != null
                    && typeof(App.Catalog.ExampleSpec[]).IsAssignableFrom(examplesForLlm.ReturnType))
                {
                    var specs = (App.Catalog.ExampleSpec[]?)examplesForLlm.Invoke(null, null)
                        ?? System.Array.Empty<App.Catalog.ExampleSpec>();
                    examples = specs
                        .Select(s => new Data.@this(s.UserIntent, App.Catalog.ExampleRenderer.Render(s, this)))
                        .ToList();
                }
                else
                {
                    examples = parameterType.GetCustomAttributes<modules.ExampleAttribute>()
                        .Select(e => new Data.@this(e.Plang, e.Mapping))
                        .ToList();
                }

                var returnType = DescribeReturnType(parameterType);

                // Action-level description from [System.ComponentModel.Description]
                var descAttr = parameterType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                var actionDescription = descAttr?.Description;

                // Module-level description: cache per namespace, search all types in namespace on first miss
                if (!moduleDescriptionCache.TryGetValue(ns, out var moduleDescription))
                {
                    moduleDescription = null;
                    foreach (var type in GetAllTypesInNamespace(ns))
                    {
                        var modDesc = type.GetCustomAttribute<modules.ModuleDescriptionAttribute>();
                        if (modDesc != null)
                        {
                            moduleDescription = modDesc.Description;
                            break;
                        }
                    }
                    moduleDescriptionCache[ns] = moduleDescription;
                }

                // Per-action modifier classification from [Modifier] on the class
                bool isModifier = parameterType.GetCustomAttribute<modules.ModifierAttribute>() != null;

                result.Add(new Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = ns,
                    ActionName = actionName,
                    ParameterSchema = parameterType,
                    Parameters = parameters,
                    Cacheable = cacheable,
                    Examples = examples,
                    ReturnType = returnType,
                    Description = actionDescription,
                    ModuleDescription = moduleDescription,
                    IsModifier = isModifier
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
    /// Reads the Run() method's return type. If it returns a concrete Data subtype,
    /// reflects its public properties for the builder summary. Returns null for plain Data.
    /// </summary>
    private static List<Data.@this>? DescribeReturnType(System.Type actionType)
    {
        var runMethod = actionType.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
        if (runMethod == null) return null;

        var returnType = runMethod.ReturnType;

        // Unwrap Task<T> → T
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            returnType = returnType.GetGenericArguments()[0];

        // Plain Data — no extra properties to describe
        if (returnType == typeof(Data.@this)) return null;

        // Must be a Data subclass
        if (!typeof(Data.@this).IsAssignableFrom(returnType)) return null;

        // Collect public properties that are NOT on the base Data class
        var baseProps = typeof(Data.@this).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var properties = new List<Data.@this>();
        foreach (var prop in returnType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (baseProps.Contains(prop.Name)) continue;
            var typeName = Utils.TypeMapping.GetTypeName(prop.PropertyType);
            properties.Add(new Data.@this(prop.Name, typeName));
        }

        return properties.Count > 0 ? properties : null;
    }

    /// <summary>
    /// Returns default values for an action's parameters that aren't already provided.
    /// Checks IConfigure&lt;TConfig&gt; first, falls back to [Default] attributes.
    /// </summary>
    public List<Data.@this>? GetDefaults(string module, string actionName, HashSet<string> excludeParams)
    {
        var actionType = GetActionType(module, actionName);
        if (actionType == null) return null;

        // IConfigure<TConfig> → instantiate config, read property defaults
        foreach (var iface in actionType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(modules.IConfigure<>))
            {
                var configType = iface.GetGenericArguments()[0];
                object? instance;
                try { instance = Activator.CreateInstance(configType); }
                catch (MissingMethodException) { break; } // No parameterless constructor — fall through to [Default] attributes
                if (instance == null) break;

                var defaults = new List<Data.@this>();
                foreach (var prop in configType.GetProperties())
                {
                    if (excludeParams.Contains(prop.Name)) continue;
                    var value = prop.GetValue(instance);
                    if (value == null) continue;
                    defaults.Add(new Data.@this(prop.Name.ToLowerInvariant(), value));
                }
                return defaults;
            }
        }

        // [Default] attributes
        var attrDefaults = new List<Data.@this>();
        foreach (var prop in actionType.GetProperties())
        {
            if (excludeParams.Contains(prop.Name)) continue;
            var attrs = prop.GetCustomAttributes(typeof(modules.DefaultAttribute), false);
            if (attrs.Length == 0) continue;
            attrDefaults.Add(new Data.@this(prop.Name.ToLowerInvariant(),
                ((modules.DefaultAttribute)attrs[0]).Value));
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
    /// True when <paramref name="propType"/> is <c>Data&lt;T&gt;</c> (or its nullable
    /// wrap) for a T that implements <see cref="App.Variables.IRawNameResolvable"/>.
    /// The property type is the carrier of "this slot names a variable" — the catalog
    /// builder uses this to mark <c>%var%</c>-shape parameters in the LLM prompt.
    /// </summary>
    private static bool IsVariableNameSlot(Type propType)
    {
        var underlying = Nullable.GetUnderlyingType(propType) ?? propType;
        if (!underlying.IsGenericType) return false;
        if (underlying.GetGenericTypeDefinition() != typeof(Data.@this<>)) return false;
        var inner = underlying.GetGenericArguments()[0];
        return typeof(App.Variables.IRawNameResolvable).IsAssignableFrom(inner);
    }
}

/// <summary>
/// Single registry entry — either a Type (per-call instantiation) or a shared Instance.
/// </summary>
public record ActionEntry(Type? Type, IAction? Instance)
{
    public ICodeGenerated? Create()
    {
        if (Instance is ICodeGenerated cg)
            return cg;

        if (Type != null && typeof(ICodeGenerated).IsAssignableFrom(Type))
            return (ICodeGenerated)Activator.CreateInstance(Type)!;

        return null;
    }
}
