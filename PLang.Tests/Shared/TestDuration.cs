namespace PLang.Tests;

/// <summary>A span made the way a program makes one: through duration's door, born short with the app's
/// {duration, short} type, as <paramref name="context"/>'s types hold it.</summary>
public static class TestDuration
{
    public static global::app.type.item.duration.@this Duration(this global::app.actor.context.@this context, System.TimeSpan span)
        => global::app.type.item.duration.@this.Create(span, context);
}
