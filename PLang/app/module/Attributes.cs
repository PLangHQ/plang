namespace app.module;

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
/// Marks a slot holding a goal.call action as a callback that sets a variable before the held call runs.
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
