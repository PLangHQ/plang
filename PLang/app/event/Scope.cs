namespace app.@event;

/// <summary>Whose an event is: the actor that bound it (it runs only while that actor runs), or the app's
/// (it runs for every actor).</summary>
[global::app.Attributes.PlangType("scope"), global::app.Attributes.Choice]
public enum Scope
{
    actor,
    app,
}
