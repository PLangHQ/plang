using System.Text.Json;

namespace app.type.item.kind.dict;

/// <summary>
/// The dict kind — a raw CLR <see cref="System.Collections.IDictionary"/> host (a POCO's
/// <c>Dictionary&lt;string,string&gt;</c>, e.g. <c>goal.InputParameters</c>). Owns key-descend
/// (<c>%goal.InputParameters.foo%</c>), entry-enumeration, and object-Output. Also owns the
/// OUTBOUND convert: <c>dict</c> knows how to build itself FROM a json source, so <c>%x% as
/// dict</c> (a clr(json) → native dict) is here, not on json — reusing the universal DOM
/// narrower (<c>item.serializer.json.Parse</c>). Claims <c>IDictionary</c> by assignable match.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this(global::app.actor.context.@this? context = null) : base("dict", context) { }

    public override System.Type? ClrForm => typeof(System.Collections.IDictionary);

    public override (bool, object?) Descend(object obj, string key, global::app.actor.context.@this ctx)
    {
        var dict = (System.Collections.IDictionary)obj;
        return dict.Contains(key) ? (true, dict[key]) : (false, null);
    }

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
    {
        foreach (System.Collections.DictionaryEntry e in (System.Collections.IDictionary)obj)
            yield return Data(e.Key.ToString() ?? "", e.Value, null, ctx);
    }

    public override async global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
    {
        writer.BeginObject();
        foreach (System.Collections.DictionaryEntry e in (System.Collections.IDictionary)obj)
        {
            writer.Name(e.Key.ToString() ?? "");
            if (e.Value is { } v) await WriteReflected(writer, v, mode, ctx!);
            else writer.Value(null);
        }
        writer.EndObject();
    }

    public override async global::System.Threading.Tasks.ValueTask<global::app.data.@this> Convert(
        global::app.data.@this source, global::app.actor.context.@this ctx)
    {
        object? value = await source.Value();
        JsonElement element = value switch
        {
            global::app.type.clr.@this c when c.Value is JsonElement je => je,
            JsonElement je2 => je2,
            _ => default,
        };
        if (element.ValueKind is JsonValueKind.Undefined)
            return ctx.Error(new global::app.error.ServiceError(
                $"cannot convert {source.Type?.Name} into dict — source is not json", "ConvertFailed", 400));

        object? parsed = new global::app.type.item.serializer.json(ctx).Parse(element);
        return ctx.Ok(parsed);
    }
}
