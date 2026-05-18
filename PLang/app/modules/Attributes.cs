namespace app.modules;

/// <summary>
/// Marks a class as a PLang action handler. The source generator discovers these
/// and generates ICodeGenerated dispatch code.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ActionAttribute : Attribute
{
    /// <summary>Action name used in .pr files (e.g., "set", "read"). Defaults to class name.</summary>
    public string? Name { get; }
    /// <summary>Whether the builder can cache this action's result. Default true.</summary>
    public bool Cacheable { get; set; } = true;

    public ActionAttribute() { }
    public ActionAttribute(string name) => Name = name;
}

/// <summary>
/// Specifies a default value for an action parameter when the builder omits it.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DefaultAttribute : Attribute
{
    /// <summary>The default value to use when the parameter is not provided.</summary>
    public object? Value { get; }

    public DefaultAttribute(object? value) => Value = value;
}

/// <summary>
/// Marks a GoalCall property as a callback that injects variables into the called goal.
/// The Injects property names the variable the callback receives (e.g., "chunk" for streaming data).
/// The user can rename it in PLang syntax (e.g., "on stream call HandleChunk myData=%chunk%").
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class GoalCallbackAttribute : Attribute
{
    public string Injects { get; }
    public GoalCallbackAttribute(string injects) => Injects = injects;
}

/// <summary>
/// Marks a property for automatic injection from the runtime escape-hatch
/// <c>app.Code</c>. The source generator emits <c>app.Code.Get&lt;T&gt;()</c>
/// in <c>ExecuteAsync</c> for each <c>[Code]</c> property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class CodeAttribute : Attribute { }

/// <summary>
/// Marks a Data? property that must be initialized (non-null) before Run() is called.
/// The source generator validates this at dispatch time.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IsInitiatedAttribute : Attribute { }

/// <summary>
/// Marks a property that must not be null. The source generator validates this at dispatch time.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IsNotNullAttribute : Attribute { }

/// <summary>
/// Action classes that implement this interface can validate LLM-generated parameters
/// during the build. The builder calls ValidateBuild after the LLM produces parameters,
/// returning errors that the LLM can use to self-correct.
/// </summary>
public interface IBuildValidatable
{
    /// <summary>
    /// Validates LLM-generated parameters. Returns null if valid,
    /// or an error message describing what's wrong so the LLM can fix it.
    /// </summary>
    static abstract string? ValidateBuild(List<data.@this> parameters);
}

/// <summary>
/// Describes the module as a whole. Apply to exactly one class per module namespace
/// (the alphabetically first action by convention). Rendered as a module-level heading
/// in the builder action catalog so the LLM understands the module's purpose.
/// Individual action classes declare themselves modifiers via [Modifier] — the catalog
/// renders modifier actions in their own section per-action, not per-module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModuleDescriptionAttribute : Attribute
{
    public string Description { get; }
    public ModuleDescriptionAttribute(string description) => Description = description;
}

/// <summary>
/// Provides a PLang step example and its expected parameter mapping for the builder.
/// Multiple examples per action help the LLM map natural language to the correct parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ExampleAttribute : Attribute
{
    /// <summary>PLang step text (e.g., "before step, call LogStep").</summary>
    public string Plang { get; }
    /// <summary>Expected parameter mapping (e.g., "Type=BeforeStep, GoalToCall=LogStep").</summary>
    public string Mapping { get; }

    public ExampleAttribute(string plang, string mapping)
    {
        Plang = plang;
        Mapping = mapping;
    }
}
