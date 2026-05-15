namespace app.modules;

/// <summary>
/// Marks a handler class as an action modifier. The builder groups modifier actions
/// onto their preceding executable action and sorts by Order before writing the .pr file.
/// Lower Order = outermost wrapper in the runtime fold.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModifierAttribute : Attribute
{
    /// <summary>
    /// Nesting order within a fold. Lower values wrap outer; higher values wrap closer to the action.
    /// Current assignments: timeout=1, cache=2, error=3.
    /// </summary>
    public int Order { get; init; }
}
