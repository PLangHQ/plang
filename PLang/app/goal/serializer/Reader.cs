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
        string text = System.Text.Encoding.UTF8.GetString(reader.RawValue());
        if (string.IsNullOrEmpty(text)) return new global::app.type.@null.@this("goal", kind);
        return System.Text.Json.JsonSerializer.Deserialize<global::app.goal.@this>(
            text, global::app.type.catalog.@this.GoalReadOptions(ctx.Context))!;
    }
}
