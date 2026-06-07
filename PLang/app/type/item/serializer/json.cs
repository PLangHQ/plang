namespace app.type.item.serializer;

/// <summary>
/// Reader for the <c>item</c> shape encoded as <c>json</c>. Born-native names a json
/// payload of unknown shape <c>item</c> (the universal value), so a read of <c>.json</c>
/// stamps <c>{item, json}</c> and materializes through <c>(item, json)</c> here. Same
/// json-string → CLR decode as the <c>(object, json)</c> reader — delegated, one pipeline.
/// </summary>
public static class json
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
        => global::app.type.@object.serializer.json.Read(raw, kind, ctx);
}
