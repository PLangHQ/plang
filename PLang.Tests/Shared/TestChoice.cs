namespace PLang.Tests;

/// <summary>A choice made the way a program's makers make one: born holding the app's <c>{choice, &lt;set&gt;}</c>, as
/// <paramref name="context"/>'s types hold it.</summary>
public static class TestChoice
{
    /// <summary>The option <paramref name="value"/>.</summary>
    public static global::app.type.item.choice.@this<T> Choice<T>(this global::app.actor.context.@this context, T value)
        where T : notnull
        => new(value, context.App.type.list[typeof(global::app.type.item.choice.@this<T>)]);

    /// <summary>The option <paramref name="symbol"/> names (throws FormatException naming the options when none).</summary>
    public static global::app.type.item.choice.@this<T> Choice<T>(this global::app.actor.context.@this context, string symbol)
        where T : notnull
        => global::app.type.item.choice.@this<T>.Parse(symbol, context.App.type.list[typeof(global::app.type.item.choice.@this<T>)]);
}
