using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.type.choice;

/// <summary>
/// Plain-JSON view of a <see cref="@this{T}"/> — bare option name string. A
/// factory because the type is generic; STJ knows the closed option type at the leaf.
/// </summary>
public sealed class JsonFactory : JsonConverterFactory
{
    public override bool CanConvert(System.Type t)
        => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(@this<>);

    public override JsonConverter CreateConverter(System.Type t, JsonSerializerOptions options)
    {
        var optionType = t.GetGenericArguments()[0];
        var conv = typeof(Json<>).MakeGenericType(optionType);
        return (JsonConverter)System.Activator.CreateInstance(conv)!;
    }
}

public sealed class Json<T> : JsonConverter<@this<T>>
    where T : notnull
{
    public override void Write(Utf8JsonWriter writer, @this<T> value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    public override @this<T> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        => @this<T>.FromName(reader.GetString() ?? "", null);
}
