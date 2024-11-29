using System.Text;

namespace PLang.Services.Channels.Serializers;

public class TextSerializer
{
    private readonly Encoding _encoding;

    public string ContentType => "text/plain";

    public TextSerializer(Encoding? encoding = null)
    {
        _encoding = encoding ?? Encoding.UTF8;
    }

    public Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default)
    {
        string text = obj?.ToString() ?? string.Empty;
        byte[] data = _encoding.GetBytes(text);
        return Task.FromResult(data);
    }

    public Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        string text = _encoding.GetString(data);
        object result = Convert.ChangeType(text, typeof(T));
        return Task.FromResult((T)result);
    }
}