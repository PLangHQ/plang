namespace app.Diagnostics;

/// <summary>
/// Diagnostic output — a value as a human reads it in an assertion message, a test report line, a debug dump.
/// A plang value answers by its own face; a scalar by its text; anything else (a record, a raw collection) is
/// taken as its plang value and written through the json writer in the Debug view, where a <c>[Sensitive]</c>
/// member shows masked: a reader tells "not set" from "set but hidden", and never sees the secret.
/// Never <c>value.ToString()</c> on an arbitrary object — a record's auto-ToString prints every field.
/// </summary>
public static class Format
{
    public static async System.Threading.Tasks.ValueTask<string> Value(object? value, global::app.actor.context.@this context)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        if (value is global::app.type.item.@this { IsLeaf: true } leaf) return leaf.ToString() ?? leaf.GetType().Name;
        if (value.GetType().IsPrimitive || value is decimal or System.Enum or System.DateTime or System.DateTimeOffset
                or System.TimeSpan or System.Guid)
            return value.ToString() ?? value.GetType().Name;
        var item = value as global::app.type.item.@this ?? global::app.type.item.@this.Create(value, context);
        using var stream = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions
                     {
                         Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                     }))
            await item.Output(new global::app.type.item.kind.json.Writer(utf8, global::app.View.Debug, emitsSchema: false),
                global::app.View.Debug, context);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
