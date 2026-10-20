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
    // The subclass's own static Offer, read once at construction (as a type reads a declared fact), never per Offers call.
    private readonly System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>? _offer;

    /// <summary>The kind <paramref name="name"/> of <paramref name="owner"/> (the app's <typeparamref name="T"/>), whose
    /// values are <paramref name="class"/>.</summary>
    public @this(string name, System.Type @class, global::app.type.@this owner) : base(name, owner)
    {
        _class = @class;
        _offer = @class.GetProperty("Offer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null)
            as System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>;
    }

    public override System.Type? ClrForm => _class;

    /// <summary>The class values of this kind are: the subclass itself (a <c>{text, separator}</c> value is a
    /// <c>separator.@this</c>, a <c>{screen, window}</c> a <c>window</c>) — so Create, Offer and the read use it,
    /// never the abstract or wider family class (decision 787: a kind's class is the class its values are).</summary>
    public override System.Type? Of(System.Type? type) => _class;

    /// <summary>What a value of this kind can be in a step, as the decider is offered it: the subclass's own
    /// <c>Offer</c> (a separator's named values — line, comma) alongside the step's own variables (<c>%x%</c>),
    /// since the set is open. The subclass names them on its own static <c>Offer</c>, read the same way a type
    /// reads a class's facts; a subclass that names none offers the step's variables alone (the base).</summary>
    public override async System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
    {
        var variables = await base.Offers(step);
        return _offer is { } offer ? [.. offer, .. variables] : variables;
    }
}
