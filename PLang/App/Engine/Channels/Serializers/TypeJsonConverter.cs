using System.Text.Json;
using System.Text.Json.Serialization;
using Type = App.Engine.Variables.Type;

namespace App.Engine.Channels.Serializers;

/// <summary>
/// Serializes <see cref="Type"/> as a plain JSON string (e.g. "string", "int").
/// Deserializes a JSON string back into a <see cref="Type"/> instance.
/// </summary>
public sealed class TypeJsonConverter : JsonConverter<Type?>
{
    public override Type? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? null : new Type(value);
    }

    public override void Write(Utf8JsonWriter writer, Type? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value);
    }
}
