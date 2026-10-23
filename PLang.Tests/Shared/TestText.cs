namespace PLang.Tests;

/// <summary>A text made the way a program's makers make one: born holding the app's text type, as
/// <paramref name="context"/>'s types hold it.</summary>
public static class TestText
{
    public static global::app.type.item.text.@this Text(this global::app.actor.context.@this context, string value)
        => new(value, context.App.type.list[typeof(global::app.type.item.text.@this)]);
}
