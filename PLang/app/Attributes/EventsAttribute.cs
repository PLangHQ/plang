namespace app.Attributes;

/// <summary>
/// The events this plang type's items have besides the core's (<c>start</c>, <c>create</c>, … <c>write</c>): a window's
/// <c>opened</c> and <c>closed</c>, an element's <c>click</c>. They bind like any event (<c>on %window% closed, call X</c>)
/// and are raised by the module that owns the type.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
public sealed class EventsAttribute(params string[] names) : System.Attribute
{
    /// <summary>The events' names.</summary>
    public System.Collections.Generic.IReadOnlyList<string> Names { get; } = names;
}
