namespace PLang.Runtime;

public partial class ChannelData
{
    private readonly Dictionary<string, object?> _data = new();
    
    public ErrorInfo? Error { get; set; }
    
    public object? this[string channel]
    {
        get => _data.TryGetValue(channel, out var val) ? val : null;
        set => _data[channel] = value;
    }
    
    public bool Contains(string channel) => _data.ContainsKey(channel);
    
    public IEnumerable<string> ChannelNames => _data.Keys;
}
