using App.Channels.Serializers;
using App.Errors;

namespace App.Channels.Channel.Stream;

/// <summary>
/// Concrete Stream-backed channel. Wraps a <see cref="System.IO.Stream"/> for I/O.
/// Extends <see cref="Session.@this"/> — Ask blocks reading from the stream until input arrives.
///
/// Console streams (stdin/stdout/stderr) and arbitrary handed-in streams (HTTP response bodies,
/// memory streams in tests) all use this concrete.
/// </summary>
public sealed class @this : Session.@this
{
    private readonly bool _ownsStream;
    private Serializers.@this? _serializers;

    /// <summary>The underlying stream this channel reads/writes.</summary>
    public global::System.IO.Stream Stream { get; }

    /// <summary>
    /// Optional serializer registry override. Defaults to a process-wide instance lazily.
    /// Stage 6 promotes this to <c>App.Serializers</c>; for now Stream channels carry the registry.
    /// </summary>
    public Serializers.@this Serializers
    {
        get => _serializers ??= new Serializers.@this();
        init => _serializers = value;
    }

    public @this(string name, global::System.IO.Stream stream,
        ChannelDirection direction = ChannelDirection.Bidirectional,
        bool ownsStream = true)
    {
        Name = name;
        Stream = stream;
        Direction = direction;
        _ownsStream = ownsStream;
    }

    /// <summary>Read-only stream channel (e.g. stdin).</summary>
    public static @this Input(string name, global::System.IO.Stream stream, bool ownsStream = false)
        => new(name, stream, ChannelDirection.Input, ownsStream)
        {
            Role = global::App.Channels.Channel.Role.@this.Input
        };

    /// <summary>Write-only stream channel (e.g. stdout, stderr).</summary>
    public static @this Output(string name, global::System.IO.Stream stream, bool ownsStream = false)
        => new(name, stream, ChannelDirection.Output, ownsStream)
        {
            Role = global::App.Channels.Channel.Role.@this.Output
        };

    /// <summary>In-memory bidirectional channel (testing / capture).</summary>
    public static @this Memory(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
        => new(name, new MemoryStream(), direction, ownsStream: true);

    public override bool CanRead => IsOpen && Direction != ChannelDirection.Output && Stream.CanRead;
    public override bool CanWrite => IsOpen && Direction != ChannelDirection.Input && Stream.CanWrite;

    public override async Task<Data.@this> WriteCore(Data.@this data, CancellationToken ct = default)
    {
        if (!CanWrite)
            return Data.@this.FromError(new ServiceError(
                $"Channel '{Name}' does not support writing", "ChannelReadOnly", 400));

        try
        {
            await Serializers.SerializeAsync(new SerializeOptions
            {
                Stream = Stream,
                Data = data.Value,
                ContentType = Mime,
                CancellationToken = ct
            });
            return Data.@this.Ok();
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return Data.@this.FromError(new ServiceError(
                $"Failed to write to channel '{Name}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    public override async Task<Data.@this> ReadCore(CancellationToken ct = default)
    {
        if (!CanRead)
            return Data.@this.FromError(new ServiceError(
                $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400));

        try
        {
            var text = await ReadAllTextAsync(ct);
            return Data.@this.Ok(text);
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return Data.@this.FromError(new ServiceError(
                $"Failed to read from channel '{Name}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    public override async Task<Data.@this> AskCore(Data.@this prompt, CancellationToken ct = default)
    {
        // Session-style ask: write the prompt (if any), then read a line.
        // Timeout enforced via the per-channel Timeout config.
        if (prompt.Value != null)
        {
            var writeRes = await WriteCore(prompt, ct);
            if (!writeRes.Success) return writeRes;
        }

        if (!CanRead)
            return Data.@this.FromError(new ServiceError(
                $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Timeout);

        try
        {
            var reader = new StreamReader(Stream, leaveOpen: true);
            var line = await reader.ReadLineAsync(timeoutCts.Token);
            return Data.@this.Ok(line ?? string.Empty);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return Data.@this.FromError(new ServiceError(
                $"Channel '{Name}' ask timed out after {Timeout}", "AskTimeout", 408));
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return Data.@this.FromError(new ServiceError(
                $"Failed to ask on channel '{Name}': {ex.Message}", "AskError") { Exception = ex });
        }
    }

    // --- Convenience surface kept for v1 callers ---------------------------------

    public async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead)
            throw new InvalidOperationException($"Channel '{Name}' does not support reading");

        if (Stream is MemoryStream ms)
            return ms.ToArray();

        using var buffer = new MemoryStream();
        await Stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    public async Task WriteBytesAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!CanWrite)
            throw new InvalidOperationException($"Channel '{Name}' does not support writing");

        await Stream.WriteAsync(data, cancellationToken);
        await Stream.FlushAsync(cancellationToken);
    }

    public async Task<string> ReadAllTextAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(cancellationToken);
        return global::System.Text.Encoding.UTF8.GetString(bytes);
    }

    public async Task WriteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var bytes = global::System.Text.Encoding.UTF8.GetBytes(text);
        await WriteBytesAsync(bytes, cancellationToken);
    }

    public override void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (_ownsStream) Stream.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (_ownsStream) await Stream.DisposeAsync();
    }
}
