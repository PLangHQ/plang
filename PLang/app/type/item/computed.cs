namespace app.type.item;

/// <summary>
/// A value computed fresh at every use — system variables like <c>%!Now%</c>
/// whose truth changes per read. The factory's result is lifted to its item
/// form on each ask, with the asker's context; <see cref="Cacheable"/> is false so
/// the holding <c>Data</c> never rebinds — there is nothing to keep, by design (the
/// same rule that keeps a template render from being stored). Stores no context: its
/// holder (<c>DynamicData</c>) passes its own.
/// </summary>
public sealed class computed : @this
{
    private readonly System.Func<object?> _factory;
    private readonly string? _declared;
    private readonly string? _declaredKind;

    public computed(System.Func<object?> factory,
        string? declaredTypeName = null, string? declaredKind = null)
    {
        _factory = factory ?? throw new System.ArgumentNullException(nameof(factory));
        _declared = declaredTypeName;
        _declaredKind = declaredKind;
    }

    /// <summary>The declared answer type when the cell advertises one
    /// (%Now% is a datetime); "item" when undeclared — the computation's
    /// answer carries its own truth at each use.</summary>
    protected internal override global::app.type.@this Type
        => new(_declared ?? NamespaceTail(GetType())) { Kind = _declaredKind is { } k ? new global::app.type.kind.@this(k) : null };

    public override bool Cacheable => false;

    /// <summary>Never final — the door computes a fresh answer on every read.</summary>
    internal override bool IsFinal => false;

    /// <summary>The current answer — the factory's result lifted to its item form with the
    /// asker's context (a host the factory returns, <c>%!app%</c>, resolves its kind through it).</summary>
    internal @this Compute(actor.context.@this context)
        => global::app.type.item.@this.Create(_factory(), context);

    public override System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this data)
        => System.Threading.Tasks.ValueTask.FromResult(Compute(data.Context));

    /// <summary>A computed materialises itself (runs the factory) before navigating —
    /// the result (e.g. a datetime) then navigates by its own rules.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
        => await Compute(parent.Context).Get(parent, key);

    /// <summary>Writes the current answer, lifted with the writer's context.</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => Compute(context ?? throw Contextless()).Output(writer, mode, context);

    /// <summary>"In memory now" for a computed IS the current computation, which needs the
    /// asker's context to lift — its holder answers it (<c>DynamicData.Peek</c>).</summary>
    public override object? Peek() => throw Contextless();

    /// <summary>Shared by reference — a computed cell recomputes fresh at each use, so there
    /// is nothing to copy.</summary>
    protected internal override @this Clone() => this;

    public override bool IsTruthy() => throw Contextless();
    public override string ToString() => $"(computed {Type.Name})";

    private static System.InvalidOperationException Contextless() => new(
        "a computed value answers only with the asker's context — read it through its Data.");
}
