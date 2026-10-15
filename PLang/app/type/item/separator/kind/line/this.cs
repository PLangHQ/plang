namespace app.type.item.separator.kind.line;

/// <summary>A new line — a text's lines (<c>split %x% into lines</c>).</summary>
public sealed class @this : global::app.type.item.separator.kind.@this
{
    public @this() : base("line") { }

    protected override System.Collections.Generic.IReadOnlyList<string>? OwnAlias => ["lines", "newline"];

    public override string Characters => "\n";
}
