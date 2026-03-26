namespace PLang.Runtime2.modules;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ActionAttribute : Attribute
{
    public string? Name { get; }
    public bool Cacheable { get; set; } = true;

    public ActionAttribute() { }
    public ActionAttribute(string name) => Name = name;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DefaultAttribute : Attribute
{
    public object? Value { get; }

    public DefaultAttribute(object? value) => Value = value;
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class VariableNameAttribute : Attribute { }

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

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ProviderAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IsInitiatedAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IsNotNullAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ExampleAttribute : Attribute
{
    public string Plang { get; }
    public string Mapping { get; }

    public ExampleAttribute(string plang, string mapping)
    {
        Plang = plang;
        Mapping = mapping;
    }
}
