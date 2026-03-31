using System.Collections.Concurrent;
using System.Reflection;
using PLang.Runtime2.modules;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;

namespace PLang.Runtime2.Engine.Modules;

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
        Discover(typeof(@this).Assembly, "PLang.Runtime2.modules");
    }

    /// <summary>
    /// Discovers [Action]-attributed ICodeGenerated types in an assembly and registers them.
    /// External DLLs call this via module.add.
    /// </summary>
    public int Discover(Assembly assembly, string? baseNamespace = null)
    {
        baseNamespace ??= "PLang.Runtime2.modules";
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
    /// Resolves an action for execution. Returns (action, null) or (null, error).
    /// </summary>
    public (ICodeGenerated? Action, IError? Error) GetCodeGenerated(
        string module, string actionName, PLangContext context)
    {
        if (!_modules.TryGetValue(module, out var actions) ||
            !actions.TryGetValue(actionName, out var entry))
            return (null, ActionError.NotFound($"Action '{module}.{actionName}'", context));

        var action = entry.Create();
        if (action == null)
            return (null, new ActionError(
                $"Action '{module}.{actionName}' does not implement ICodeGenerated",
                context, "ActionError", 500) { ActionModule = module, ActionName = actionName });

        return (action, null);
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

    public int Count => _modules.Values.Sum(a => a.Count);

    /// <summary>
    /// All registered instances (for disposal on engine shutdown).
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
    /// EngineModules owns this because it knows its own types.
    /// </summary>
    public Goals.Goal.Steps.Step.Actions.@this Describe()
    {
        var result = new Goals.Goal.Steps.Step.Actions.@this();
        var nCtx = new NullabilityInfoContext();

        foreach (var ns in Names)
        {
            foreach (var actionName in GetActions(ns))
            {
                var parameterType = GetActionType(ns, actionName);
                if (parameterType == null) continue;

                var parameters = new List<Memory.Data>();

                foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;
                    if (prop.GetCustomAttribute<modules.ProviderAttribute>() != null) continue;

                    var typeName = Utility.TypeMapping.GetTypeName(prop.PropertyType);

                    bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null;
                    if (!isNullable && !prop.PropertyType.IsValueType)
                        isNullable = nCtx.Create(prop).WriteState == NullabilityState.Nullable;
                    if (isNullable && !typeName.EndsWith("?"))
                        typeName += "?";

                    var validValues = Utility.TypeMapping.GetValidValues(prop.PropertyType);
                    if (validValues != null)
                        typeName += $"({string.Join("|", validValues)})";

                    var hasVar = prop.GetCustomAttribute<modules.VariableNameAttribute>() != null;
                    var defaultAttr = prop.GetCustomAttribute<modules.DefaultAttribute>();

                    var desc = hasVar ? $"@var {typeName}" : typeName;
                    if (defaultAttr != null)
                        desc += $" = {FormatDefault(defaultAttr.Value)}";

                    parameters.Add(new Memory.Data(prop.Name, desc));
                }

                bool cacheable = true;
                var actionAttr = parameterType.GetCustomAttribute<modules.ActionAttribute>();
                if (actionAttr != null)
                    cacheable = actionAttr.Cacheable;

                var examples = parameterType.GetCustomAttributes<modules.ExampleAttribute>()
                    .Select(e => new Memory.Data(e.Plang, e.Mapping))
                    .ToList();

                var returnType = DescribeReturnType(parameterType);

                result.Add(new Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = ns,
                    ActionName = actionName,
                    ParameterSchema = parameterType,
                    Parameters = parameters,
                    Cacheable = cacheable,
                    Examples = examples,
                    ReturnType = returnType
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Reads the Run() method's return type. If it returns a concrete Data subtype,
    /// reflects its public properties for the builder summary. Returns null for plain Data.
    /// </summary>
    private static List<Memory.Data>? DescribeReturnType(System.Type actionType)
    {
        var runMethod = actionType.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes);
        if (runMethod == null) return null;

        var returnType = runMethod.ReturnType;

        // Unwrap Task<T> → T
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            returnType = returnType.GetGenericArguments()[0];

        // Plain Data — no extra properties to describe
        if (returnType == typeof(Memory.Data)) return null;

        // Must be a Data subclass
        if (!typeof(Memory.Data).IsAssignableFrom(returnType)) return null;

        // Collect public properties that are NOT on the base Data class
        var baseProps = typeof(Memory.Data).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var properties = new List<Memory.Data>();
        foreach (var prop in returnType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (baseProps.Contains(prop.Name)) continue;
            var typeName = Utility.TypeMapping.GetTypeName(prop.PropertyType);
            properties.Add(new Memory.Data(prop.Name, typeName));
        }

        return properties.Count > 0 ? properties : null;
    }

    /// <summary>
    /// Returns default values for an action's parameters that aren't already provided.
    /// Checks IConfigure&lt;TConfig&gt; first, falls back to [Default] attributes.
    /// </summary>
    public List<Memory.Data>? GetDefaults(string module, string actionName, HashSet<string> excludeParams)
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
                catch { break; } // No parameterless constructor — fall through to [Default] attributes
                if (instance == null) break;

                var defaults = new List<Memory.Data>();
                foreach (var prop in configType.GetProperties())
                {
                    if (excludeParams.Contains(prop.Name)) continue;
                    var value = prop.GetValue(instance);
                    if (value == null) continue;
                    defaults.Add(new Memory.Data(prop.Name.ToLowerInvariant(), value));
                }
                return defaults;
            }
        }

        // [Default] attributes
        var attrDefaults = new List<Memory.Data>();
        foreach (var prop in actionType.GetProperties())
        {
            if (excludeParams.Contains(prop.Name)) continue;
            var attrs = prop.GetCustomAttributes(typeof(modules.DefaultAttribute), false);
            if (attrs.Length == 0) continue;
            attrDefaults.Add(new Memory.Data(prop.Name.ToLowerInvariant(),
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
