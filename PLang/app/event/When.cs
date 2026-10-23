namespace app.@event;

/// <summary>Which side of an event a binding runs on: before it, or after it.</summary>
[global::app.Attributes.PlangType("when"), global::app.Attributes.Choice]
public enum When
{
    before,
    after,
}
