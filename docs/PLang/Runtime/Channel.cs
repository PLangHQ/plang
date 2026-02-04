namespace PLang.Runtime;

public partial class Channel : Stream
{
    public string Name { get; }
    public CallStack CallStack { get; }
    
    private readonly MemoryStream _buffer;
    private readonly SerializerRegistry _serializers;
    private Goal? _boundGoal;
    private Func<object, Task<GoalResult>>? _handler;
    
    public Channel(string name, SerializerRegistry serializers)
    {
        Name = name;
        CallStack = new CallStack();
        _buffer = new MemoryStream();
        _serializers = serializers;
    }
    
    public void Bind(Goal goal)
    {
        _boundGoal = goal;
        _handler = null;
    }
    
    public void Bind(Func<object, Task<GoalResult>> handler)
    {
        _handler = handler;
        _boundGoal = null;
    }
    
    public void Unbind()
    {
        _boundGoal = null;
        _handler = null;
    }
    
    public bool IsBound => _boundGoal != null || _handler != null;
    
    // Fire and forget
    public async Task WriteAsync(object data, string serializer = "json")
    {
        if (_handler != null)
        {
            await _handler(data);
        }
        else if (_boundGoal != null)
        {
            await _boundGoal.Run(_boundGoal.Steps.FirstOrDefault()?.ParentGoal?.CallStack != null 
                ? _boundGoal.Steps.First().ParentGoal!.CallStack!.Current?.Goal?.Path != null 
                    ? ModuleRegistry.Get("callgoal").Engine 
                    : null! 
                : null!, data);
        }
        else
        {
            _serializers[serializer].Serialize(data, _buffer);
        }
    }
    
    // Request/response - blocks until response
    public async Task<GoalResult> WriteAndWaitAsync(object data, string serializer = "json")
    {
        if (_handler != null)
        {
            return await _handler(data);
        }
        else if (_boundGoal != null)
        {
            // Need engine reference - this is a limitation we'll address
            return GoalResult.Error("Bound goal execution requires engine context");
        }
        
        // No handler - just write to buffer and return success
        _serializers[serializer].Serialize(data, _buffer);
        return GoalResult.Success();
    }
    
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
}
