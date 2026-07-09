using System.Text.Json;

namespace app.type.item.kind.json;

/// <summary>
/// The json kind — navigates a <see cref="JsonElement"/> by the plang path. A container hop
/// stays a <see cref="JsonElement"/>; the landed node builds its child: an object / array
/// becomes a <c>clr</c> (its kind derives to json again), a scalar becomes its plang scalar
/// (string→text, long/double→number, true/false→bool, null→the null citizen). Its CLR form is
/// <see cref="JsonElement"/> — the one place that fact lives, so a <c>clr(JsonElement)</c>
/// resolves to this kind and navigates here, not by reflection.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this(global::app.actor.context.@this? context = null) : base("json", context) { }

    public override System.Type? ClrForm => typeof(JsonElement);

    public override (bool, object?) Descend(object obj, string key, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var byName))
            return (true, byName);
        if (e.ValueKind == JsonValueKind.Array
            && int.TryParse(key, out var n) && n >= 0 && n < e.GetArrayLength())
            return (true, e[n]);
        return (false, null);
    }

    public override global::app.data.@this Data(string name, object? node,
        global::app.data.@this? parent, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)node!;
        return e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? new global::app.data.@this(name, new global::app.type.clr.@this(e, ctx, this), parent: parent, context: ctx)
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

    // Writing a child onto an immutable json object: materialize it into a mutable dict whose
    // members STAY lazy (each a Data over its own child node), then set the new key. The json
    // content becomes the dict's keys, never the JsonElement's BCL surface.
    public override global::app.type.item.@this Set(
        object host, string key, object? value, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)host;
        var dict = new global::app.type.dict.@this(ctx);
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())
                dict.Set(Data(p.Name, p.Value, null, ctx));
        dict.Set(key, value);
        return dict;
    }

    // Raw json text/bytes → a clr(json), through the single json parse owner.
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(
        object raw, global::app.actor.context.@this ctx)
        => new(ctx.Ok(global::app.type.@object.serializer.json.Read(
            raw, "json", new global::app.type.reader.ReadContext(ctx))));

    // Materialize this json content INTO the CLR host target asks for. json owns the format
    // bridge — its element becomes a reader — and the `*` kind owns the shape (the [Store] host
    // walk driven off that reader). The door a clr(json) delegates to instead of terminal-lowering.
    public override object? Clr(object host, System.Type target, global::app.actor.context.@this ctx)
    {
        var utf8 = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(((JsonElement)host).GetRawText()));
        utf8.Read();
        var reader = new global::app.channel.serializer.json.Reader(utf8);
        return new global::app.type.item.kind.reflection.@this().Read(ref reader, target, new global::app.type.reader.ReadContext(ctx));
    }

    // A json value writes its own raw json inline — NEVER reflecting the JsonElement's BCL props.
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
