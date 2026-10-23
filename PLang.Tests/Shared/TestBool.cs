namespace PLang.Tests;

/// <summary>A bool made the way a program's makers make one: born holding the app's bool type, as
/// <paramref name="context"/>'s types hold it.</summary>
public static class TestBool
{
    public static global::app.type.item.@bool.@this Bool(this global::app.actor.context.@this context, bool value)
        => new(value, context.App.type.list[typeof(global::app.type.item.@bool.@this)]);
}
