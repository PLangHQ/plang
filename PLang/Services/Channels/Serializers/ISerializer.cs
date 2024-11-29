namespace PLang.Services.Channels.Serializers;

public interface ISerializer
{
    Task<byte[]> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default);
    Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default);

}