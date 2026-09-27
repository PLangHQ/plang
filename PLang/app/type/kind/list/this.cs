namespace app.type.kind.list;

/// <summary>
/// A type's kinds as the full types they make — <c>%!app.type.number.kind.list%</c> is
/// <c>[{number, int}, {number, long}, …]</c>: every entry carries its type.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.type.@this>
{
    public @this(System.Collections.Generic.IEnumerable<global::app.type.@this> types)
        : base(types.Select(t => (global::app.type.item.@this)t)) { }
}
