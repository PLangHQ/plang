namespace PLang.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class MethodSettingsAttribute : Attribute
{
    public MethodSettingsAttribute(bool canBeCached = true, bool canHaveErrorHandling = true, bool canBeAsync = true)
    {
        CanBeCached = canBeCached;
        CanHaveErrorHandling = canHaveErrorHandling;
        CanBeAsync = canBeAsync;
    }

    public bool CanBeCached { get; set; }
    public bool CanHaveErrorHandling { get; set; }
    public bool CanBeAsync { get; set; }
}