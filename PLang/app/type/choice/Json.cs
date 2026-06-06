using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.choice;

/// <summary>
/// Plain-JSON view of a <see cref="@this{TEnum}"/> — bare option name string. A
/// factory because the type is generic; STJ knows the closed enum at the leaf.
/// </summary>
public sealed class JsonFactory : JsonConverterFactory
{
    public override bool CanConvert(System.Type t)
        => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(@this<>);

    public override JsonConverter CreateConverter(System.Type t, JsonSerializerOptions options)
    {
        var enumType = t.GetGenericArguments()[0];
        var conv = typeof(Json<>).MakeGenericType(enumType);
        return (JsonConverter)System.Activator.CreateInstance(conv)!;
    }
}

public sealed class Json<TEnum> : JsonConverter<@this<TEnum>>
    where TEnum : struct, System.Enum
{
    public override void Write(Utf8JsonWriter writer, @this<TEnum> value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());

    public override @this<TEnum> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? "";
        return new @this<TEnum>(System.Enum.Parse<TEnum>(s, ignoreCase: true));
    }
}
