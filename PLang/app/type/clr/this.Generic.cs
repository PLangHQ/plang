namespace app.type.clr;

/// <summary>
/// Typed view of a foreign host — <c>clr&lt;T&gt;</c>, where <c>T</c> is the host CLR type.
/// A thin subclass of the untyped <see cref="@this"/>: it adds NO storage or behavior, only
/// the host-type tag so a slot can declare <c>Data&lt;clr&lt;app&gt;&gt;</c> and read
/// <see cref="Value"/> strongly-typed — no cast, no reflection guess. The clr analogue of
/// <c>list&lt;T&gt;</c>: the host never has to become a plang item type to be carried strictly.
/// </summary>
public sealed class @this<T> : @this, global::app.type.item.ICreate<@this<T>>
    where T : class
{
    public @this(T value, global::app.actor.context.@this context, global::app.type.kind.@this? kind = null)
        : base(value, context, kind) { }

    /// <summary>The strongly-typed host — the point of the typed carrier.</summary>
    public new T Value => (T)base.Value;

    /// <summary>
    /// Retag a host to <c>clr&lt;T&gt;</c>. An existing clr wrapping a <typeparamref name="T"/>
    /// (or already a <c>clr&lt;T&gt;</c>) is carried by IDENTITY — the same live reference,
    /// never re-created: a live host like the app is a singleton, so a copy would lose its
    /// wiring. A source that is not a T host declines (null).
    /// </summary>
    public static @this<T>? Create(object? value, global::app.data.@this data)
    {
        if (value is @this<T> already) return already;
        if (value is @this c && c.Value is T typed) return new @this<T>(typed, data.Context!, c.Kind);
        return null;
    }
}
