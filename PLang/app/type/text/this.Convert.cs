using System.Text.Json;

namespace app.type.text;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>text</c> knows how to make itself from any value. A string (or a
    /// <c>text</c>) passes through; a structured value (dict / list / JSON DOM)
    /// becomes its JSON serialization — that is what <c>text/json</c> means: json
    /// TEXT; a scalar primitive stringifies invariantly. An opaque domain object
    /// has no honest textual form — fail rather than leak its CLR type name.
    /// (<paramref name="kind"/> is accepted for signature parity with other types'
    /// Convert hooks; text's kind is a hint and doesn't change the textual form.)
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is null) return global::app.data.@this.Ok(value);
        // Always born-native: text builds a `text` value. A .NET edge that needs the
        // raw CLR string unwraps with .Clr<string>().
        global::app.data.@this S(string? str) => global::app.data.@this.Ok((@this)(str ?? string.Empty));
        if (value is string s) return S(s);
        if (value is @this self) return S(self.Clr<string>());

        // Native dict/list value types are not IDictionary/IEnumerable, but their
        // [JsonConverter] renders the canonical {}/[] textual form — text/json means
        // json TEXT, so route them through serialization like any other structured value.
        if (value is app.type.dict.@this
            || value is app.type.list.@this
            || value is System.Collections.IDictionary
            || value is JsonElement
            || value is System.Text.Json.Nodes.JsonNode
            || (value is System.Collections.IEnumerable && value is not byte[]))
            return S(JsonSerializer.Serialize(value));

        if (value is System.IConvertible)
            return S(System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));

        return global::app.data.@this.FromError(new global::app.error.Error(
            $"Cannot bind a {value.GetType().Name} to text — it has no textual form.",
            "TypeConversionFailed", 400));
    }
}
