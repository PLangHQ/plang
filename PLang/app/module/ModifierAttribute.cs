namespace app.module;

/// <summary>
/// Marks a handler class as an action modifier — it wraps the action it is written around.
/// The runtime composes an action's modifiers in their written order (outermost first);
/// Order is the nesting the builder teaches and gives a flat answer.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModifierAttribute : Attribute
{
    /// <summary>
    /// The nesting a flat answer is given. Lower values wrap outer; higher values wrap closer to the action.
    /// Current assignments: on.error=1, cache.wrap=2, timeout.after=3.
    /// </summary>
    public int Order { get; init; }
}
