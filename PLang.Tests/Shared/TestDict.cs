namespace PLang.Tests;

/// <summary>A dict made the way a program's makers make one: born holding the app's dict type, as
/// <paramref name="context"/>'s types hold it.</summary>
public static class TestDict
{
    /// <summary>An empty dict.</summary>
    public static global::app.type.item.dict.@this Dict(this global::app.actor.context.@this context)
        => new(context.App.type.list[typeof(global::app.type.item.dict.@this)]);
}
