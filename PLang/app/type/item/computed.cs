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
    private readonly System.Func<actor.context.@this, @this?> _factory;

    /// <summary>A value found at each read by <paramref name="factory"/>, handed the asker's context; born holding
    /// <paramref name="type"/> — the app's type it is declared as, else the app's <c>item</c> (it may be anything).</summary>
    public computed(System.Func<actor.context.@this, @this?> factory, global::app.type.@this type)
        : base(type)
    {
        _factory = factory ?? throw new System.ArgumentNullException(nameof(factory));
    }

    public override bool Cacheable => false;

    /// <summary>Never final — the door computes a fresh answer on every read.</summary>
    internal override bool IsFinal => false;

    /// <summary>The current answer — what the factory finds with the asker's context; nothing found is the
    /// null value.</summary>
    internal @this Compute(actor.context.@this context)
        => _factory(context) ?? global::app.type.item.@this.Create(null, context);

    public override System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this data)
        => System.Threading.Tasks.ValueTask.FromResult(Compute(data.Context));

    /// <summary>A computed materialises itself (runs the factory) before navigating —
    /// the result (e.g. a datetime) then navigates by its own rules.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
        => await Compute(parent.Context).Get(parent, key);

    /// <summary>Writes the current answer, lifted with the writer's context.</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
        => writer.Kind.pair.Output(Compute(context ?? throw Contextless()), writer, mode, context);

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
