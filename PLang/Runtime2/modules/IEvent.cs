namespace PLang.Runtime2.modules;

/// <summary>
/// Capability interface — declares that this object has events (before/after).
/// The MemoryStack injects context on the Event property during dot-path traversal.
/// </summary>
public interface IEvent
{
    Event Event { get; set; }
}
