namespace app.type.number;

public sealed partial class @this
{
    /// <summary>
    /// TRANSIENT hub face — the convert hub still calls a type's <c>Convert</c> until it dies (the
    /// Stage-2 hub deletion). It delegates to the plang-typed construction: the pure core for an
    /// underived value, the declared kind otherwise. All the real building lives on <c>Create</c> and
    /// the kind classes now; this just adapts the hub's <c>(object, kind, context)</c> to it.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is null) return context.Ok(value);
        if (value is not global::app.type.item.@this item)
            return context.Error(new global::app.error.Error(
                $"Cannot convert {value.GetType().Name} to number.", "NumberConversionFailed", 400));
        try
        {
            var n = kind is not null && Kinds.TryGetValue(kind, out var k) ? k.Create(item) : Create(item);
            return n is null
                ? context.Error(new global::app.error.Error(
                    $"Cannot convert {item.Mint().Name} to number.", "NumberConversionFailed", 400))
                : context.Ok(n);
        }
        catch (System.Exception ex) when (ex is System.InvalidCastException or System.FormatException or System.OverflowException)
        {
            return context.Error(new global::app.error.Error(ex.Message, "NumberConversionFailed", 400) { Exception = ex });
        }
    }
}
