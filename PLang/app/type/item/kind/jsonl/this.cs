using System.Text.Json;

namespace app.type.item.kind.jsonl;

/// <summary>
/// The JSON Lines kind — content of one json value per line (<c>.jsonl</c>, <c>application/x-ndjson</c>). Read, it is
/// the list of its lines, each read by json's own <c>Parse</c>; written, each record is compact on its own line, ended
/// by LF (<see cref="Writer"/>).
/// </summary>
public sealed class @this : global::app.type.kind.@this<global::app.type.item.@this>
{
    public @this(global::app.type.@this owner) : base("jsonl", owner) { }

    public override System.Collections.Generic.IReadOnlyList<string> Mime => ["application/jsonl", "application/x-ndjson"];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => [".jsonl", ".ndjson"];
    public override bool Compressible => true;

    /// <summary>jsonl content is text.</summary>
    public override bool IsText => true;

    /// <summary>jsonl content written: its records, a line each.</summary>
    public override async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View view,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
    {
        try
        {
            await data.Output(new Writer(stream, view, context));
            return context.Ok();
        }
        // a record that refused itself is the encode's answer, as in json
        catch (global::app.error.AppException ex) { return context.Error(ex.Error); }
        catch (System.Exception ex) when (ex is JsonException or System.NotSupportedException or System.InvalidOperationException)
        {
            return context.Error(new global::app.error.ServiceError($"JSON Lines serialize failed: {ex.Message}", "JsonSerializeError", 400) { Exception = ex });
        }
    }

    // Raw characters/bytes → the list of their lines' values. A CR before the LF is part of the end, a blank line is
    // skipped, and a line json can't read is refused naming its number. A non-raw carrier declines.
    public override global::app.type.item.@this? Parse(object raw, global::app.actor.context.@this ctx)
    {
        if (raw is not (string or byte[])) return null;
        var characters = new global::app.type.item.text.@this(raw, ctx.App.type.list[typeof(global::app.type.item.text.@this)]).ToString();
        var json = ctx.App.type.list["item"].kind["json"]!;
        var values = new System.Collections.Generic.List<global::app.type.item.@this>();
        var lines = characters.Split('\n');
        for (var at = 0; at < lines.Length; at++)
        {
            var line = lines[at].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            try { values.Add(json.Parse(line, ctx)!); }
            catch (JsonException shape)
            {
                throw new global::app.error.AppException(new global::app.error.@this(
                    $"jsonl content doesn't read: line {at + 1} is no json value: {shape.Message}", "MaterializeFailed", 400) { Exception = shape }, shape);
            }
        }
        return new global::app.type.item.list.@this(values, ctx.App.type.list[typeof(global::app.type.item.list.@this)]);
    }

    /// <summary>jsonl characters opened: the list of their lines' values, or why they don't read — MaterializeFailed,
    /// naming the line; never a throw.</summary>
    public override global::app.data.@this? Open(string characters, global::app.actor.context.@this context)
    {
        try { return context.Ok(Parse(characters, context)); }
        catch (global::app.error.AppException refused) { return context.Error(refused.Error); }
    }
}
