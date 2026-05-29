using System.Text;

namespace app.channel.serializer;

/// <summary>
/// Plain text serializer - emits data.Value as its string representation.
/// Falls back to JSON for complex types so that e.g. List&lt;T&gt; outputs proper JSON
/// instead of "System.Collections.Generic.List`1[...]".
/// </summary>
public sealed class Text : ISerializer
{
    public string Type => "text/plain";
    public string Extension => ".txt";

    private readonly Encoding _encoding;
    private readonly global::app.channel.serializer.Json _jsonFallback;

    public Text(Encoding? encoding = null, global::app.channel.serializer.Json? jsonFallback = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
        _jsonFallback = jsonFallback ?? new global::app.channel.serializer.Json();
    }

    public async Task<data.@this> SerializeAsync(Stream stream, data.@this data, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = data.Value;
            if (!IsSimpleType(value))
                return await _jsonFallback.SerializeAsync(stream, data, cancellationToken);

            var bytes = _encoding.GetBytes((value?.ToString() ?? "") + Environment.NewLine);
            await stream.WriteAsync(bytes, cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Text serialize failed: {ex.Message}", "TextSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(stream, _encoding, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return global::app.data.@this.Ok(text);
        }
        catch (Exception ex) when (ex is IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Text deserialize failed: {ex.Message}", "TextDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await DeserializeAsync(stream, cancellationToken);
        if (!result.Success) return global::app.data.@this<T>.From(result);
        var converted = ConvertFromString(result.Value as string ?? "", typeof(T));
        return global::app.data.@this<T>.Ok(converted is T typed ? typed : default!);
    }

    public data.@this<string> Serialize(data.@this data)
    {
        var value = data.Value;
        if (IsSimpleType(value)) return global::app.data.@this<string>.Ok(value?.ToString() ?? "");
        return _jsonFallback.Serialize(data);
    }

    public data.@this Deserialize(string s)
        => global::app.data.@this.Ok(s);

    public data.@this<T> Deserialize<T>(string s)
    {
        var result = ConvertFromString(s, typeof(T));
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
