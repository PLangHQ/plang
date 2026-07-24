namespace app.type.item.tag.serializer;

/// <summary>
/// Read side for <c>tag</c> in the reader registry — the "type reads itself" path. A raw
/// string / text normalizes into a tag via <c>tag.Create</c> (trim + case-fold live on the tag).
/// </summary>
public static class Default
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.item.tag.@this.Create(raw,
            new global::app.data.@this("", new global::app.type.item.@null.@this("tag", kind), context: ctx.Context));
}
