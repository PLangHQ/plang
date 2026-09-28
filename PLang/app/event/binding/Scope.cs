namespace app.@event.binding;

/// <summary>Whose a binding is: the actor that bound it (it fires only while that actor runs), or the app's
/// (it fires for every actor).</summary>
[global::app.Attributes.PlangType("scope")]
public enum Scope
{
    actor,
    app,
}
