using Data = global::app.data.@this;

namespace app.module.http.type.entity;

/// <summary>
/// PLang <c>entity</c> value — what a request's body is, as HTTP names it: its mime type and its character
/// encoding, <c>{mime: "application/json", encoding: "utf-8"}</c> (Content-Type, Content-Encoding). Each member
/// left out keeps its default, which lives here once. The http module's own; the option that holds it is a
/// request's <c>content</c> (the type name <c>content</c> is a file reference's).
/// </summary>
[global::app.Attributes.PlangType("entity")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.type.item.IDefault<@this>
{
    public static string Example => "{mime: \"application/json\", encoding: \"utf-8\"}";
    public static string Description => "What a request's body is: its mime type and its character encoding.";
    public static string Shape => "object";

    /// <summary>The body's mime type — <c>%!http.request.setting.content.mime%</c>; names the format it is written in.</summary>
    [Out, Store] public global::app.type.item.text.@this Mime { get; }

    /// <summary>The body's character encoding — <c>%!http.request.setting.content.encoding%</c>.</summary>
    [Out, Store] public global::app.type.item.text.@this Encoding { get; }

    /// <summary>The defaults: json, in utf-8.</summary>
    public @this() : this("application/json", "utf-8") { }

    /// <summary>A request that says nothing of its body's content sends json, in utf-8.</summary>
    public static @this Default => new();

    internal @this(global::app.type.item.text.@this mime, global::app.type.item.text.@this encoding)
    {
        Mime = mime;
        Encoding = encoding;
    }

    public override bool IsLeaf => false;

    /// <summary>As a step writes it — the catalog shows the default this way.</summary>
    public override string ToString() => $"{{mime: \"{Mime}\", encoding: \"{Encoding}\"}}";

    /// <summary>A content is made from a dict of its members — any left out keeps its default; a member that is no
    /// member of a content declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this content) return content;
        if (raw is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a request's content is {mime, encoding}", "ContentInvalid", 400));
            return null;
        }
        var fresh = new @this();
        global::app.type.item.text.@this mime = fresh.Mime;
        global::app.type.item.text.@this encoding = fresh.Encoding;
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "mime" when entry.Peek()?.ToString() is { Length: > 0 } m: mime = m; break;
                case "encoding" when entry.Peek()?.ToString() is { Length: > 0 } e: encoding = e; break;
                default:
                    data.Fail(new global::app.error.Error(
                        $"a request's content has mime and encoding — not {entry.Name}", "ContentInvalid", 400));
                    return null;
            }
        return new @this(mime, encoding);
    }

    /// <summary>Writes itself as its dict.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("mime"); writer.String(Mime.ToString());
        writer.Name("encoding"); writer.String(Encoding.ToString());
        writer.EndObject();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
