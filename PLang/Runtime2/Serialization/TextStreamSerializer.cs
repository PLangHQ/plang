using System.Text;

namespace PLang.Runtime2.Serialization;

/// <summary>
/// Plain text serializer - converts objects to their string representation.
/// </summary>
public sealed class TextStreamSerializer : ISerializer
{
    public string ContentType => "text/plain";
    public string FileExtension => ".txt";

    private readonly Encoding _encoding;

    public TextStreamSerializer(Encoding? encoding = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
    }

    public async Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
    {
        var text = value?.ToString() ?? "";
        var bytes = _encoding.GetBytes(text);
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
        return value?.ToString() ?? "";
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
