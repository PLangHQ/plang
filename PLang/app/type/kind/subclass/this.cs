namespace app.type.kind.subclass;

/// <summary>
/// A kind a family's subclass is — the family declares it has kinds (<c>[Kinds]</c>), and each class under it that
/// makes values is one of them, by its own name: a query's <c>where</c>, an input's <c>mouse</c>. Its values ride as
/// that class. A kind of its family <typeparamref name="T"/> (decision 774): the family names the owner, the subclass
/// names the CLR form its values ride as. The value class stays a <typeparamref name="T"/> subclass — it is the
/// kind, not a <c>kind&lt;T&gt;</c> itself — so this is the kind OBJECT that stands for it.
/// </summary>
public sealed class @this<T> : global::app.type.kind.@this<T> where T : global::app.type.item.@this
{
    private readonly System.Type _class;

    /// <summary>The kind <paramref name="name"/> of <typeparamref name="T"/>, whose values are <paramref name="class"/>.</summary>
    public @this(string name, System.Type @class) : base(name) => _class = @class;

    public override System.Type? ClrForm => _class;
}
