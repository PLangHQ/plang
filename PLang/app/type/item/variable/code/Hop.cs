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

    protected Hop(string text) => Text = text;

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
        => System.Threading.Tasks.ValueTask.FromResult(context.Error(new global::app.error.Error(
            $"'{Text}' can't be written to — a {Kind} answers a value, it holds none.", "VariableNotWritable", 400)));

    /// <summary>A hop writes its own <c>.pr</c> form (<see cref="Write"/>) in every view.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    /// <summary>Its <c>.pr</c> form: one object under its kind — <c>{"property": "address"}</c>.</summary>
    public override void Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name(Kind);
        Piece(writer);
        writer.EndObject();
    }

    /// <summary>What it writes under its kind.</summary>
    protected abstract void Piece(global::app.type.format.IWriter writer);

    public override string ToString() => Text;
}
