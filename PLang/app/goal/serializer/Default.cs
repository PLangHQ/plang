namespace app.goal.serializer;

/// <summary>
/// Reader for the <c>goal</c> shape — a <c>.pr</c> payload
/// (<c>application/plang-goal</c>) materializing back into a
/// <see cref="app.goal.@this"/>. This re-houses the <c>type.Convert(text,
/// typeof(goal))</c> the file read used to do eagerly inside
/// <c>FilePath.ReadText</c>; under lazy reads a <c>.pr</c> lands as raw json
/// and reconstructs to a Goal only when its value is touched (e.g.
/// <c>GoalCall</c> loading a sub-goal).
///
/// <para>There is no <c>Write</c> mirror — a Goal renders through the channel
/// serializer's json writer like any structured value; the reader is the
/// type-owned half that turns the <c>.pr</c> source form back into the Goal,
/// using the context-bound converter so nested <c>Path</c> fields (Goal.Path,
/// GoalCall.PrPath) land fully wired.</para>
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        // Content off I/O rides as binary bytes; the .pr is json text — decode
        // through the text type (it owns bytes→string), then DESERIALIZE the Goal
        // directly. Reading a .pr is the goal's own wire read, not a value coercion
        // — it does NOT route through the type-conversion hub (Type.Convert). A parse
        // failure throws JsonException (path + line), surfaced by source.Value.
        if (raw is not (string or byte[]) || ctx.Context == null) return raw;
        string text = new global::app.type.text.@this(raw).ToString();
        if (string.IsNullOrEmpty(text)) return null;
        return System.Text.Json.JsonSerializer.Deserialize<global::app.goal.@this>(
            text, global::app.type.catalog.@this.GoalReadOptions(ctx.Context));
    }
}
