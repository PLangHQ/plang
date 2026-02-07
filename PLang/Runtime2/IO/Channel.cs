namespace PLang.Runtime2.IO;

/// <summary>
/// Direction of a channel (input, output, or bidirectional).
/// </summary>
public enum ChannelDirection
{
    Input,
    Output,
    Bidirectional
}

/// <summary>
/// Represents a named I/O channel backed by a Stream.
/// </summary>
public sealed class Channel : IAsyncDisposable, IDisposable
{
    public string Name { get; }
    public Stream Stream { get; }
    public ChannelDirection Direction { get; }
    public string? ContentType { get; set; }
    public bool IsOpen { get; private set; }
    public DateTime Created { get; }
    public IDictionary<string, object> Metadata { get; }

    private readonly bool _ownsStream;

    public Channel(string name, Stream stream, ChannelDirection direction = ChannelDirection.Bidirectional, bool ownsStream = true)
    {
        Name = name;
        Stream = stream;
        Direction = direction;
        _ownsStream = ownsStream;
        IsOpen = true;
        Created = DateTime.UtcNow;
        Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates an input channel from a stream.
    /// </summary>
    public static Channel Input(string name, Stream stream) => new(name, stream, ChannelDirection.Input);

    /// <summary>
    /// Creates an output channel from a stream.
    /// </summary>
    public static Channel Output(string name, Stream stream) => new(name, stream, ChannelDirection.Output);

    /// <summary>
    /// Creates a memory-backed channel.
    /// </summary>
    public static Channel Memory(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
        => new(name, new MemoryStream(), direction);

    /// <summary>
    /// Creates a channel from a file.
    /// </summary>
    public static Channel File(string name, string path, FileMode mode = FileMode.OpenOrCreate)
    {
        var direction = mode == FileMode.Open ? ChannelDirection.Input :
                       mode == FileMode.Create || mode == FileMode.CreateNew ? ChannelDirection.Output :
                       ChannelDirection.Bidirectional;
        var stream = new FileStream(path, mode, direction == ChannelDirection.Input ? FileAccess.Read :
            direction == ChannelDirection.Output ? FileAccess.Write : FileAccess.ReadWrite);
        return new Channel(name, stream, direction);
    }

    /// <summary>
    /// Checks if reading is supported.
    /// </summary>
    public bool CanRead => IsOpen && Direction != ChannelDirection.Output && Stream.CanRead;

    /// <summary>
    /// Checks if writing is supported.
    /// </summary>
    public bool CanWrite => IsOpen && Direction != ChannelDirection.Input && Stream.CanWrite;

    /// <summary>
    /// Reads all bytes from the channel.
    /// </summary>
	
	// check: can we take adventage of the Span<T> Memory<T> thingys, I dont know it well enough
	// read over full class to see
    public async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead)
            throw new InvalidOperationException($"Channel '{Name}' does not support reading");

        if (Stream is MemoryStream ms)
        {
            return ms.ToArray();
        }

        using var buffer = new MemoryStream();
        await Stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>
    /// Writes bytes to the channel.
    /// </summary>
    public async Task WriteBytesAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!CanWrite)
            throw new InvalidOperationException($"Channel '{Name}' does not support writing");

        await Stream.WriteAsync(data, cancellationToken);
        await Stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Reads all text from the channel.
    /// </summary>
    public async Task<string> ReadAllTextAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(cancellationToken);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Writes text to the channel.
    /// </summary>
    public async Task WriteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        await WriteBytesAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Closes the channel.
    /// </summary>
    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        if (_ownsStream)
        {
            Stream.Dispose();
        }
    }

    public void Dispose()
    {
        Close();
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        if (_ownsStream)
        {
            await Stream.DisposeAsync();
        }
    }
}
