namespace app.type.kind.empty;

/// <summary>
/// A type's kind when it has none — a bare <c>text</c> has text's empty kind. It knows the type it
/// belongs to, so its <c>list</c> is that type's kinds. Its name is empty and it is never written.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly string _owner;

    public @this(string owner) : base("") => _owner = owner;

    protected internal override string Owner => _owner;
}
