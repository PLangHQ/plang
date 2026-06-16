namespace app.type.item;

/// <summary>
/// A value computed fresh at every use — system variables like <c>%!Now%</c>
/// whose truth changes per read. The factory's result is lifted to its item
/// form on each ask; <see cref="Cacheable"/> is false so the holding
/// <c>Data</c> never rebinds — there is nothing to keep, by design (the same
/// rule that keeps a template render from being stored).
/// </summary>
public sealed class computed : @this, module.IContext
{
    private readonly System.Func<object?> _factory;
    private readonly string? _declared;
    private readonly string? _declaredKind;

    /// <summary>Stamped by the holding <c>Data</c> when its Context is set, so the
    /// computation's result lifts with context — a host object the factory returns
    /// (<c>%!app%</c>) can then resolve its registry name (its kind) on mint.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    public computed(System.Func<object?> factory, string? declaredTypeName = null, string? declaredKind = null)
    {
        _factory = factory ?? throw new System.ArgumentNullException(nameof(factory));
        _declared = declaredTypeName;
        _declaredKind = declaredKind;
    }

    /// <summary>The declared answer type when the cell advertises one
    /// (%Now% is a datetime); "item" when undeclared — the computation's
    /// answer carries its own truth at each use.</summary>
    protected internal override global::app.type.@this Mint()
        => new(_declared ?? NamespaceTail(GetType())) { Kind = _declaredKind };

    public override bool Cacheable => false;

    public override System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this asking)
        => System.Threading.Tasks.ValueTask.FromResult(Compute());

    /// <summary>Peek computes too — "in memory now" for a computed value IS the
    /// current computation (no I/O, no parse; the factory is a pure read).</summary>
    public override object? Peek() => Compute().Peek();

    public override bool IsTruthy() => Compute().IsTruthy();
    public override string ToString() => Compute().ToString() ?? "";

    private @this Compute()
    {
        var raw = _factory();
        return global::app.data.@this.Lift(raw, Context);
    }
}
