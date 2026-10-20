using System.Text.Json;

namespace app.type.item.kind.json;

/// <summary>
/// The json kind — a value of kind json rests as its raw bytes until a use opens it (decision 792): its
/// untyped materialize answers an unread <c>{item, json}</c> source (<see cref="Load"/>), and a use parses it
/// once into a plang value through <see cref="Parse"/> (an object into a dict, an array into a list, a scalar
/// into its native) — no <c>JsonElement</c> carrier, no navigator.
/// </summary>
public sealed class @this : global::app.type.kind.@this<global::app.type.item.@this>
{
    public @this(global::app.type.@this owner) : base("json", owner) { }

    public override System.Collections.Generic.IReadOnlyList<string> Mime => ["application/json", "text/json"];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => [".json"];
    public override bool Compressible => true;

    /// <summary>json content is text.</summary>
    public override bool IsText => true;

    /// <summary>A json writer — plain json, or plang's schema-on json — writes json: json content goes into it
    /// as the token it already is.</summary>
    public override bool Owns(global::app.type.format.IWriter writer)
        => ReferenceEquals(writer.Kind, this) || writer.Kind is global::app.type.item.wire.kind.plang.@this;

    /// <summary>A test run's report in json: the run's tests, written as json.</summary>
    public override string? Report => "results.json";

    // json RESTS (ruling 6, decision 792): its untyped materialize does NOT parse — an unread {item, json}
    // answers itself, so a courier (content.Value, a variable set, passing a value on, a url's or a render's
    // result) carries it unread. Only a USE opens it: navigation, enumeration, `is`, a typed ask each parse once
    // (through Parse) and narrow. The source form IS the rest: the source materialize sees a source come back
    // and answers itself.
    public override System.Threading.Tasks.ValueTask<global::app.data.@this?> Load(
        object raw, global::app.actor.context.@this ctx)
        // The source carries THIS kind as the format its raw is in — the object the raw was decoded by, so a
        // re-declaration keeps it and asks it to Parse directly (never a kind-name lookup across the app).
        => new(ctx.Ok(new global::app.type.item.source(raw,
            ctx.App.type.list[new global::app.type.@this("item", "json"), ctx], format: this)));

    // Every character written as itself (Icelandic letters, "—"); the ones a page reads as markup (< > & ' +)
    // still escaped, as json in an HTML page needs.
    private readonly System.Text.Encodings.Web.JavaScriptEncoder _encoder
        = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);

    /// <summary>json content written: the value alone, as json — no Data around it (the type is inferred on
    /// read). The value writes itself through the json writer.</summary>
    public override async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view = null,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
    {
        try
        {
            var face = view ?? global::app.View.Out;
            await using var utf8 = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = _encoder });
            await data.Output(new global::app.type.item.kind.json.Writer(utf8, face, context));
            await utf8.FlushAsync(ct);
            return context.Ok();
        }
        // a write that refused itself (a getter that threw, a type with no wire contract, a value nested past the
        // writer's depth — .NET's writer throws InvalidOperationException for that) is the encode's answer
        catch (global::app.error.AppException ex) { return context.Error(ex.Error); }
        catch (System.Exception ex) when (ex is JsonException or System.NotSupportedException or System.InvalidOperationException)
        {
            return context.Error(new global::app.error.ServiceError($"JSON serialize failed: {ex.Message}", "JsonSerializeError", 400) { Exception = ex });
        }
    }

    // Raw json text/bytes → a plang value. json owns its OWN parse — object is not a plang type, so this is
    // the one home. A non-raw carrier declines (null → the family reader handles it).
    public override global::app.type.item.@this? Parse(object raw, global::app.actor.context.@this ctx)
    {
        if (raw is not (string or byte[])) return null;                        // decline — not a raw json payload
        var s = new global::app.type.item.text.@this(raw, ctx.App.type.list[typeof(global::app.type.item.text.@this)]).ToString();
        if (string.IsNullOrEmpty(s)) return global::app.type.item.@this.Create(null, ctx);    // empty → the null value (NOT a decline)
        // json parses itself into a plang value (decision 774, ruling 9): an object into a dict, an array into
        // a list, each read through its own type's reader over the json token stream — no JsonElement carrier,
        // no navigator. A scalar root IS its native value (`42` a number). Verify:false — ingested content is
        // unsigned, like every nested/ingest read.
        var utf8 = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(s));
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8);
        var read = new global::app.type.reader.ReadContext(ctx, Verify: false);
        switch (reader.Peek())
        {
            case global::app.type.format.TokenKind.Object: return new global::app.type.item.dict.serializer.Reader().Read(ref reader, null, read);
            case global::app.type.format.TokenKind.Array:  return new global::app.type.item.list.serializer.Reader().Read(ref reader, null, read);
            case global::app.type.format.TokenKind.String: return global::app.type.item.@this.Create(reader.String(), ctx);
            case global::app.type.format.TokenKind.Number: return global::app.type.item.@this.Create(reader.Number(), ctx);
            case global::app.type.format.TokenKind.Bool:   return global::app.type.item.@this.Create(reader.Bool(), ctx);
            default:                                        return global::app.type.item.@this.Create(null, ctx);
        }
    }

    /// <summary>json characters opened: the json value they are, or why they don't read — MaterializeFailed, naming
    /// where; never a throw.</summary>
    public override global::app.data.@this? Open(string characters, global::app.actor.context.@this context)
    {
        try { return context.Ok(Parse(characters, context)); }
        catch (JsonException shape) { return context.Error(Unread(shape, "a json value").Error); }
    }

    // The format→type read: json bridges its content into a json stream and drives the declared
    // type's OWN reader (the same reader a .pr uses — so a nested goal.call param rides @schema:data
    // and dispatches), handing it the element kind. Verify:false — ingested content is unsigned, like
    // every nested/ingest read.
    public override object? Read(object obj, global::app.type.reader.ITypeReader reader, string? kind,
                                 global::app.actor.context.@this context)
    {
        try
        {
            var utf8 = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(((JsonElement)obj).GetRawText()));
            utf8.Read();
            var stream = new global::app.type.item.kind.json.Reader(utf8);
            return reader.Read(ref stream, kind,
                new global::app.type.reader.ReadContext(context, Verify: false));
        }
        catch (JsonException shape) { throw Unread(shape, kind ?? reader.GetType().Name); }
    }

    // json content that doesn't have the shape its target reads (an object where a list goes) — the program's
    // data, not a fault in plang: keyed like any content that doesn't read as its declared type, naming where.
    private static global::app.error.AppException Unread(JsonException shape, string target)
        => new(new global::app.error.@this(
            $"json content doesn't read as {target}: {shape.Message}"
            + (shape.Path != null || shape.LineNumber != null ? $" [at {shape.Path ?? "?"}, line {shape.LineNumber?.ToString() ?? "?"}]" : ""),
            "MaterializeFailed", 400) { Exception = shape }, shape);
}
