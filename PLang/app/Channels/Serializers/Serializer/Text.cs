using System.Text;

namespace app.Channels.Serializers.Serializer;

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
    private readonly global::app.Channels.Serializers.Serializer.Json _jsonFallback;

    public Text(Encoding? encoding = null, global::app.Channels.Serializers.Serializer.Json? jsonFallback = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
        _jsonFallback = jsonFallback ?? new global::app.Channels.Serializers.Serializer.Json();
    }

    public async Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
    {
        if (!IsSimpleType(value))
        {
            await _jsonFallback.SerializeAsync(stream, value, type, cancellationToken);
            return;
        }

        var bytes = _encoding.GetBytes((value?.ToString() ?? "") + Environment.NewLine);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    public async Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, _encoding, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return ConvertFromString(text, type);
    }

    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await DeserializeAsync(stream, typeof(T), cancellationToken);
        return result is T typed ? typed : default;
    }

    public string Serialize(object? value, Type? type = null)
    {
        if (IsSimpleType(value)) return value?.ToString() ?? "";
        return _jsonFallback.Serialize(value, type);
    }

    public object? Deserialize(string data, Type type)
    {
        return ConvertFromString(data, type);
    }

    public T? Deserialize<T>(string data)
    {
        var result = ConvertFromString(data, typeof(T));
        return result is T typed ? typed : default;
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
