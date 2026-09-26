namespace app.module;

/// <summary>
/// Marks a handler class as an action modifier — it wraps the action it is written around.
/// Order decides how the modifiers of one action nest, whatever order the programmer wrote them in:
/// the action's modifier list keeps them sorted by it (modifier.list).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ModifierAttribute : Attribute
{
    /// <summary>
    /// Lower values wrap outer; higher values wrap closer to the action. Modifiers of equal Order keep
    /// the order written (on error clauses are asked in that order). Current assignments: on.error=0
    /// (outermost: it bounds the attempts), cache.wrap=50, timeout.after=100 — gaps for new ones.
    /// </summary>
    public int Order { get; init; }
}
