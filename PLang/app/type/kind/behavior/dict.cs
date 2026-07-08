using System.Text.Json;

namespace app.type.kind.behavior;

/// <summary>
/// The dict kind's convert — proof that the OUTBOUND owns conversion: <c>dict</c> knows
/// how to build itself FROM a json source, so <c>%x% as dict</c> (a <c>clr(json)</c> →
/// native <c>dict</c>) is owned here, not by json. Reuses the universal DOM narrower
/// (<c>item.serializer.json.Parse</c>) — the one json→native walker.
/// </summary>
public sealed class dict : @this
{
    public override global::app.type.kind.@this Kind => "dict";

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
