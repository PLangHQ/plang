namespace app.type.item;

/// <summary>
/// A typed absence — a slot that carries a declared type but no value yet
/// (a tool-definition parameter the LLM will fill, a typed null result).
/// <see cref="Peek"/> answers null (there is nothing in memory), truthiness is
/// false, and the entity answers the declaration — so the wire and schema
/// surfaces keep the declared {type, kind} where the old shape stored a
/// descriptor beside a null value.
/// </summary>
public sealed class absent : @this
{
    private readonly string _type;
    private readonly string? _kind;

    public absent(string typeName, string? kind = null)
    {
        _type = string.IsNullOrWhiteSpace(typeName) ? "null" : typeName;
        _kind = kind;
    }

    protected internal override global::app.type.@this Mint()
        => new(_type) { Kind = _kind };

    public override object? Peek() => null;
    public override bool IsTruthy() => false;
    internal override object? ToRaw() => null;
    public override string ToString() => "";
}
