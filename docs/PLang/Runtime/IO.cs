using System.Collections.Concurrent;

namespace PLang.Runtime;

public partial class IO : Stream
{
    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private readonly MemoryStream _buffer = new();
    private readonly SerializerRegistry _serializers;
    
    public Channel this[string name] => GetOrCreate(name);
    public Channel Default => GetOrCreate("default");
    public CallStack CallStack { get; }
    
    public IO(SerializerRegistry serializers)
    {
        _serializers = serializers;
        CallStack = new CallStack();
    }
    
    public Channel GetOrCreate(string name)
    {
        return _channels.GetOrAdd(name, n => new Channel(n, _serializers));
    }
    
    public bool HasChannel(string name) => _channels.ContainsKey(name);
    
    public IEnumerable<string> ChannelNames => _channels.Keys;
    
    // Stream implementation
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => true;
    public override long Length => _buffer.Length;
    
    public override long Position
    {
        get => _buffer.Position;
        set => _buffer.Position = value;
    }
    
    public override void Flush() => _buffer.Flush();
    
    public override int Read(byte[] buffer, int offset, int count)
        => _buffer.Read(buffer, offset, count);
    
    public override void Write(byte[] buffer, int offset, int count)
        => _buffer.Write(buffer, offset, count);
    
    public override long Seek(long offset, SeekOrigin origin)
        => _buffer.Seek(offset, origin);
    
    public override void SetLength(long value)
        => _buffer.SetLength(value);
    
    // Object-based API
    public Task WriteAsync(object data, string channel = "default", string serializer = "json")
        => GetOrCreate(channel).WriteAsync(data, serializer);
    
    public Task<GoalResult> WriteAndWaitAsync(object data, string channel = "default", string serializer = "json")
        => GetOrCreate(channel).WriteAndWaitAsync(data, serializer);
}
