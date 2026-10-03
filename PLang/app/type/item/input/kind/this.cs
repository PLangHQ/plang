namespace app.type.item.input.kind;

/// <summary>
/// A kind of input — what the person did: <c>mouse</c>, <c>key</c>, <c>text</c>, <c>navigate</c>. A goal asks it by
/// name (<c>if %event% is mouse</c>) or reads it (<c>%event!type.kind%</c>); each variant's value reports its own.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "input";
}
