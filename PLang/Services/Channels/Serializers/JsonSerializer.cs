using System.Text.Json;

namespace PLang.Services.Channels.Serializers;

public class JsonSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default)
    {
        byte[] data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj, _options);
        return Task.FromResult(data);
    }

    public Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        T obj = System.Text.Json.JsonSerializer.Deserialize<T>(data, _options);
        return Task.FromResult(obj);
    }
}