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

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ProviderAttribute : Attribute { }
