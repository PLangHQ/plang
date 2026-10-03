namespace app.type.kind.subclass;

/// <summary>
/// A kind a family's subclass is — the family declares it has kinds (<c>[Kinds]</c>), and each class under it that
/// makes values is one of them, by its own name: a query's <c>where</c>, an input's <c>mouse</c>. Its values ride as
/// that class.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly string _family;
    private readonly System.Type _class;

    /// <summary>The kind <paramref name="name"/> of <paramref name="family"/>, whose values are <paramref name="class"/>.</summary>
    public @this(string name, string family, System.Type @class) : base(name)
    {
        _family = family;
        _class = @class;
    }

    protected internal override string Owner => _family;

    public override System.Type? ClrForm => _class;
}
