using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.item.number;

/// <summary>
/// Plain-JSON view of a <see cref="@this"/> — bare numeric token. Mirrors the
/// IWriter renderer's kind decisions: int/long/float/double/decimal as native
/// JSON numbers, the kinds beyond JSON's range (Int128/UInt128/BigInteger) as
/// their lossless invariant string. See text/Json.cs for why this exists
/// alongside the application/plang wire path.
/// </summary>
public sealed class Json : JsonConverter<@this>
{
    public override void Write(Utf8JsonWriter writer, @this value, JsonSerializerOptions options)
    {
        if (value == null) { writer.WriteNullValue(); return; }
        switch (value.BoxedValue)
        {
            case sbyte or byte or short or ushort or int:
                writer.WriteNumberValue(value.ToInt32()); return;
            case uint or long:
                writer.WriteNumberValue(value.ToInt64()); return;
            case ulong ul:
                writer.WriteNumberValue(ul); return;
            case float f:
                writer.WriteNumberValue(f); return;
            case System.Half h:
                writer.WriteNumberValue((double)h); return;
            case double d:
                writer.WriteNumberValue(d); return;
            case decimal m:
                writer.WriteNumberValue(m); return;
            default: // Int128 / UInt128 / BigInteger — lossless invariant string
                writer.WriteStringValue(value.ToString()); return;
        }
    }

    public override @this Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString() ?? "0";
            return (@this)(System.Numerics.BigInteger.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
        }
        if (reader.TryGetInt64(out var l)) return (@this)(l);
        return (@this)(reader.GetDouble());
    }
}
