namespace PLang.Runtime2.modules;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DefaultAttribute : Attribute
{
    public object? Value { get; }

    public DefaultAttribute(object? value) => Value = value;
}
