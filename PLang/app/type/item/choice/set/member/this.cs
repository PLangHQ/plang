namespace app.type.item.choice.set.member;

/// <summary>A closed set of an enum's members: its options are the members' names, a symbol names a member.</summary>
public sealed class @this : set.@this
{
    private readonly System.Type _clr;

    internal @this(System.Type clr, global::app.type.@this owner) : base(clr, Declared(clr), owner) => _clr = clr;

    protected override System.Collections.Generic.IReadOnlyList<string>? OwnValues => System.Enum.GetNames(_clr);

    public override object Member(string symbol) => System.Enum.Parse(_clr, symbol, ignoreCase: true);
}
