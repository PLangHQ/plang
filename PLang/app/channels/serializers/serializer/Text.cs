using System.Text;

namespace app.channels.serializers.serializer;

/// <summary>
/// Plain text serializer - converts objects to their string representation.
/// Falls back to JSON for complex types so that e.g. List&lt;T&gt; outputs proper JSON
/// instead of "System.Collections.Generic.List`1[...]".
/// </summary>
public sealed class Text : ISerializer
{
    public string ContentType => "text/plain";
    public string FileExtension => ".txt";

    private readonly Encoding _encoding;
    private readonly global::app.channels.serializers.serializer.Json _jsonFallback;

    public Text(Encoding? encoding = null, global::app.channels.serializers.serializer.Json? jsonFallback = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
        _jsonFallback = jsonFallback ?? new global::app.channels.serializers.serializer.Json();
    }

    public async Task<data.@this> SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsSimpleType(value))
                return await _jsonFallback.SerializeAsync(stream, value, type, cancellationToken);

            var bytes = _encoding.GetBytes((value?.ToString() ?? "") + Environment.NewLine);
            await stream.WriteAsync(bytes, cancellationToken);
            return data.@this.Ok();
        }
        catch (Exception ex) when (ex is IOException)
        {
            return data.@this.FromError(new errors.ServiceError(
                $"Text serialize failed: {ex.Message}", "TextSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(stream, _encoding, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return data.@this.Ok(ConvertFromString(text, type));
        }
        catch (Exception ex) when (ex is IOException)
        {
            return data.@this.FromError(new errors.ServiceError(
                $"Text deserialize failed: {ex.Message}", "TextDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await DeserializeAsync(stream, typeof(T), cancellationToken);
        if (!result.Success) return data.@this<T>.From(result);
        return data.@this<T>.Ok(result.Value is T typed ? typed : default!);
    }

    public data.@this<string> Serialize(object? value, Type? type = null)
    {
        if (IsSimpleType(value)) return data.@this<string>.Ok(value?.ToString() ?? "");
        return _jsonFallback.Serialize(value, type);
    }

    public data.@this Deserialize(string data, Type type)
        => global::app.data.@this.Ok(ConvertFromString(data, type));

    public data.@this<T> Deserialize<T>(string data)
    {
        var result = ConvertFromString(data, typeof(T));
        return global::app.data.@this<T>.Ok(result is T typed ? typed : default!);
    }

    private static bool IsSimpleType(object? value)
    {
        if (value == null) return true;
        var t = value.GetType();
        return t.IsPrimitive || t == typeof(string) || t == typeof(decimal)
            || t == typeof(DateTime) || t == typeof(DateTimeOffset)
            || t == typeof(Guid) || t.IsEnum;
    }

    private static object? ConvertFromString(string text, Type type)
    {
        if (string.IsNullOrEmpty(text))
            return type.IsValueType ? Activator.CreateInstance(type) : null;

        if (type == typeof(string))
            return text;

        // Handle basic types
        if (type == typeof(int) || type == typeof(int?))
            return int.TryParse(text, out var i) ? i : null;

        if (type == typeof(long) || type == typeof(long?))
            return long.TryParse(text, out var l) ? l : null;

        if (type == typeof(double) || type == typeof(double?))
            return double.TryParse(text, out var d) ? d : null;

        if (type == typeof(decimal) || type == typeof(decimal?))
            return decimal.TryParse(text, out var m) ? m : null;

        if (type == typeof(bool) || type == typeof(bool?))
            return bool.TryParse(text, out var b) ? b : null;

        if (type == typeof(DateTime) || type == typeof(DateTime?))
            return DateTime.TryParse(text, out var dt) ? dt : null;

        if (type == typeof(Guid) || type == typeof(Guid?))
            return Guid.TryParse(text, out var g) ? g : null;

        if (type == typeof(byte[]))
            return Encoding.UTF8.GetBytes(text);

        // For complex types, return the string and let the caller handle conversion
        return text;
    }
}
