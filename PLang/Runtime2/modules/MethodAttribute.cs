namespace PLang.Runtime2.modules;

/// <summary>
/// Marks an engine method as a PLang-callable primitive action.
/// The method becomes available as module.action in PLang steps.
/// Events (before/after) fire based on the module+action identity.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MethodAttribute : Attribute
{
    public string Module { get; }
    public string Action { get; }

    public MethodAttribute(string module, string action)
    {
        Module = module;
        Action = action;
    }
}
