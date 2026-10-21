namespace app.type.item.variable.code;

/// <summary>
/// One step of a variable's <see cref="@this">code</see>: it gets the value the step before it
/// answered and does its one step — the way actions pass <c>%!data%</c>. Each kind reads its own
/// piece off the <see cref="parser.@this">parser</see>, runs it, writes at it, and writes itself
/// into the <c>.pr</c> under its kind (<c>{"property": "address"}</c>).
/// </summary>
public abstract class Hop : global::app.type.item.@this, global::app.type.item.ICreate<Hop>
{
    /// <summary>The piece as written: <c>user</c>, <c>.address</c>, <c>[idx]</c>, <c>!cost</c>,
    /// <c>.replace("-", " ")</c>.</summary>
    public string Text { get; }

    /// <summary>The step after this one; null for the last.</summary>
    internal Hop? Next { get; }

    protected Hop(string text, Hop? next, global::app.type.@this type) : base(type)
    {
        Text = text;
        Next = next;
    }

    /// <summary>This step and every step after it, in order.</summary>
    internal IEnumerable<Hop> Chain => Next is null ? [this] : Next.Chain.Prepend(this);

    /// <summary>The kind the <c>.pr</c> writes it under: <c>variable</c>, <c>property</c>,
    /// <c>index</c>, <c>method</c>.</summary>
    public abstract string Kind { get; }

    /// <summary>Whether this step stays among the program's own variables — it names no <c>!</c> binding and
    /// reaches nothing but what the program set, and every value it is handed is its own too. Content read
    /// from outside holds only variables whose every step is.</summary>
    internal abstract bool IsOwn { get; }

    /// <summary>Its one step on <paramref name="previous"/> — the root has none.</summary>
    public abstract System.Threading.Tasks.ValueTask<global::app.data.@this> Start(
        global::app.data.@this? previous, global::app.actor.context.@this context);

    /// <summary>Writes <paramref name="value"/> at this step, on <paramref name="parent"/> — what the
    /// steps before it reached (the root has none). A step that can't be written answers the error.</summary>
    public virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context)
        => System.Threading.Tasks.ValueTask.FromResult(context.Error(new global::app.error.@this(
            $"'{Text}' can't be written to — a {Kind} answers a value, it holds none.", "VariableNotWritable", 400)));

    /// <summary>Its write on <paramref name="parent"/>, knowing whether that is the variable's binding
    /// (<paramref name="binding"/>). Most steps are the same either way.</summary>
    internal virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Set(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context, bool binding) => Set(parent, value, context);

    /// <summary>Its step, then the steps after it; a step that fails or reaches nothing answers there.</summary>
    internal virtual async System.Threading.Tasks.ValueTask<global::app.data.@this> Read(
        global::app.data.@this? previous, global::app.actor.context.@this context)
    {
        var data = await Start(previous, context);
        return Next is null || !data.Success || !data.IsInitialized ? data : await Next.Read(data, context);
    }

    /// <summary>The value in <paramref name="parent"/>'s slot at this step, for a write that goes further in. By default
    /// its read: a method's answer, a binding's Properties.</summary>
    internal virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Slot(
        global::app.data.@this parent, global::app.actor.context.@this context) => Start(parent, context);

    /// <summary>The binding this step writes on, when it is the step right after <paramref name="root"/>: what the root
    /// holds, born an empty dict when it holds nothing, so the write has a place to land.</summary>
    internal virtual System.Threading.Tasks.ValueTask<global::app.data.@this> Binding(
        Variable root, global::app.actor.context.@this context) => root.Ensure(context);

    /// <summary>Writes <paramref name="value"/>: the last step sets itself on <paramref name="parent"/>; any other step
    /// takes its slot in the parent and hands it to the next. A slot holding nothing leaves the write nowhere to land.
    /// Whether <paramref name="parent"/> is the variable's binding (<paramref name="binding"/>) is the root's to say.</summary>
    internal virtual async System.Threading.Tasks.ValueTask<global::app.data.@this> Write(
        global::app.data.@this? parent, object? value, global::app.actor.context.@this context, bool binding = false)
    {
        if (Next is null) return await Set(parent, value, context, binding);
        if (parent is null) return context.NotFound(Text);
        var slot = await Slot(parent, context);
        if (!slot.Success) return slot;
        if (!slot.IsInitialized)
            return context.Error(new global::app.error.@this(
                $"%{parent.Path}{Text}% holds nothing, so %{parent.Path}{string.Concat(Chain.Select(hop => hop.Text))}% has nowhere to be written",
                "VariableNotFound", 404));
        return await Next.Write(slot, value, context);
    }

    /// <summary>Its <c>.pr</c> form, in every view: one object under its kind — <c>{"property": "address"}</c>.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name(Kind);
        await Piece(writer);
        writer.EndObject();
    }

    /// <summary>What it writes under its kind.</summary>
    protected abstract System.Threading.Tasks.ValueTask Piece(global::app.type.format.IWriter writer);

    public override string ToString() => Text;
}
