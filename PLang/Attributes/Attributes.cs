namespace PLang.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class HandlesVariableAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public class BuilderHelperAttribute : Attribute
{
    public BuilderHelperAttribute(string methodName, string? explaination = null)
    {
        MethodName = methodName;
        Explaination = explaination;
    }

    public string MethodName { get; }
    public string? Explaination { get; }
}

[AttributeUsage(AttributeTargets.Method)]
public class VisibleInheritationAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public class RunOnBuildAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class DefaultValueAttribute : Attribute
{
    public DefaultValueAttribute(object? value)
    {
        Value = value;
    }

    public object? Value { get; set; }
}