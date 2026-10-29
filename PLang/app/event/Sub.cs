namespace app.@event;

/// <summary>Whether a sub app takes an event in: it does (inherit, the default), or the event stays in the app that bound
/// it (none).</summary>
[global::app.Attributes.PlangType("sub"), global::app.Attributes.Choice]
public enum Sub
{
    inherit,
    none,
}
