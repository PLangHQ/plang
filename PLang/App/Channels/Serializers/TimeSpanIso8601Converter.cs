using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace App.Channels.Serializers;

/// <summary>
/// JsonConverter that serialises <see cref="TimeSpan"/> as ISO 8601 duration strings
/// (e.g. "PT30S", "PT5M", "PT1H30M") via <see cref="XmlConvert.ToTimeSpan"/>.
///
/// Why ISO 8601: LLM zero-counting risk on int milliseconds. ISO 8601 is well-known
/// to LLMs, structured, no math required. Same converter applies anywhere TimeSpan
/// appears — register globally on JsonSerializerOptions.
/// </summary>
public sealed class TimeSpanIso8601Converter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected ISO 8601 duration string for TimeSpan, got {reader.TokenType}.");
        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw))
            throw new JsonException("Empty string is not a valid ISO 8601 duration.");
        try
        {
            return XmlConvert.ToTimeSpan(raw);
        }
        catch (FormatException ex)
        {
            throw new JsonException($"'{raw}' is not a valid ISO 8601 duration.", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(XmlConvert.ToString(value));
    }
}
