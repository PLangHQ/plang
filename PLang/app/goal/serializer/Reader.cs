namespace app.goal.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>goal</c> — a
/// <c>.pr</c> payload materializing back into a <see cref="app.goal.@this"/>. The
/// payload is JSON text; the reader takes its raw bytes off the pass and deserializes
/// the goal through the context-bound <c>GoalReadOptions</c> (Path fields land wired,
/// <c>%ref%</c> step params born as live templates).
///
/// <para>BRIDGE: goal is really a host CLR object, not a plang value type — this reader
/// exists so the read path unifies now. Final-stage cleanup (Ingi): goal rides as
/// <c>clr</c>, this reader and the goal-as-type machinery go. See
/// <c>.bot/read-path-unification/architect/v1/stage-final-cleanup.md</c>.</para>
/// </summary>
[System.Obsolete("goal rides as clr(goal); the .pr read moves to the format-agnostic reflection reader — do not add new callers.")]
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        // goal is a host — build it by reflecting its [Store] props off the reader, carry as
        // clr<goal> (an item). No STJ; the * kind's Read is the one .pr materializer. The .pr
        // payload is JSON, so open a json reader over the raw bytes (the scalar value.Reader the
        // channel hands us carries them as one token) and drive the structured walk off that.
        var raw = reader.RawValue();
        if (raw.Length == 0) return new global::app.type.item.@null.@this("goal", kind);
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var jsonReader = new global::app.channel.serializer.json.Reader(utf8);
        var goal = new global::app.type.item.kind.reflection.@this()
            .Read(ref jsonReader, typeof(global::app.goal.@this), ctx) as global::app.goal.@this;
        return goal is null
            ? new global::app.type.item.@null.@this("goal", kind)
            : new global::app.type.clr.@this<global::app.goal.@this>(goal, ctx.Context);
    }
}
