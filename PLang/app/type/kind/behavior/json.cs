using System.Text.Json;

namespace app.type.kind.behavior;

/// <summary>
/// The json kind — navigates a <see cref="JsonElement"/> by the plang path. A container
/// hop stays a <see cref="JsonElement"/>; the landed node builds its child: an object /
/// array becomes a <c>clr</c> (its kind derives to json again), a scalar becomes its
/// plang scalar (string→text, long/double→number, true/false→bool, null→the null citizen).
/// Its CLR form is <see cref="JsonElement"/> — the one place that fact lives, so a
/// <c>clr(JsonElement)</c> resolves to this kind and navigates here, not by reflection.
/// </summary>
public sealed class json : @this
{
    public override global::app.type.kind.@this Kind => "json";
    public override System.Type? ClrForm => typeof(JsonElement);

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var byName))
            return (true, byName);
        if (e.ValueKind == JsonValueKind.Array
            && int.TryParse(key, out var n) && n >= 0 && n < e.GetArrayLength())
            return (true, e[n]);
        return (false, null);
    }

    protected override global::app.data.@this Data(string name, object? node,
        global::app.data.@this? parent, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)node!;
        return e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? new global::app.data.@this(name, new global::app.type.clr.@this(e, ctx, "json"), parent: parent, context: ctx)
            : new global::app.data.@this(name, Scalar(e), parent: parent, context: ctx);
    }

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Array)
            foreach (var element in e.EnumerateArray()) yield return Data("", element, null, ctx);
        else if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject()) yield return Data(p.Name, p.Value, null, ctx);
    }

    // Raw json text/bytes → a clr(json), through the single json parse owner
    // (object/serializer/json.Read). The parse IS the validation ("is this valid json").
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(
        object raw, global::app.actor.context.@this ctx)
        => new(ctx.Ok(global::app.type.@object.serializer.json.Read(
            raw, "json", new global::app.type.reader.ReadContext(ctx))));

    // A json value writes its own raw json inline (json/plang writers emit it structurally via
    // WriteRawValue; a text writer falls its Raw back to a string) — NEVER reflecting the
    // JsonElement's BCL properties (no `valueKind` leak).
    public override global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
    {
        writer.Raw(((JsonElement)obj).GetRawText());
        return default;
    }

    // A json scalar → its raw CLR face; the Data ctor lifts it to the plang scalar.
    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
