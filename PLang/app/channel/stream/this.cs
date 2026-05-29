using app.channel.serializer;
using app.error;

namespace app.channel.stream;

/// <summary>
/// Concrete Stream-backed channel. Wraps a <see cref="System.IO.Stream"/> for I/O.
/// Extends <see cref="Session.@this"/> — Ask blocks reading from the stream until input arrives.
///
/// Console streams (stdin/stdout/stderr) and arbitrary handed-in streams (HTTP response bodies,
/// memory streams in tests) all use this concrete.
/// </summary>
public sealed class @this : global::app.channel.session.@this
{
    private readonly bool _ownsStream;

    /// <summary>The underlying stream this channel reads/writes.</summary>
    public global::System.IO.Stream Stream { get; }

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
        => new(name, stream, ChannelDirection.Input, ownsStream);

    /// <summary>Write-only stream channel (e.g. stdout, stderr).</summary>
    public static @this Output(string name, global::System.IO.Stream stream, bool ownsStream = false)
        => new(name, stream, ChannelDirection.Output, ownsStream);

    /// <summary>In-memory bidirectional channel (testing / capture).</summary>
    public static @this Memory(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
        => new(name, new MemoryStream(), direction, ownsStream: true);

    public override bool CanRead => IsOpen && Direction != ChannelDirection.Output && Stream.CanRead;
    public override bool CanWrite => IsOpen && Direction != ChannelDirection.Input && Stream.CanWrite;

    public override async Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
    {
        if (!CanWrite)
            return global::app.data.@this.FromError(new ServiceError(
                $"Channel '{Name}' does not support writing", "ChannelReadOnly", 400));

        try
        {
            var serResult = await Channels!.Serializers.SerializeAsync(new SerializeOptions
            {
                Stream = Stream,
                Data = data,
                Type = Mime,
                CancellationToken = ct
            });
            return serResult;
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return global::app.data.@this.FromError(new ServiceError(
                $"Failed to write to channel '{Name}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    public override async Task<global::app.data.@this> Read(CancellationToken ct = default)
    {
        if (!CanRead)
            return global::app.data.@this.FromError(new ServiceError(
                $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400));

        try
        {
            var text = await ReadAllTextAsync(ct);
            return global::app.data.@this.Ok(text);
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return global::app.data.@this.FromError(new ServiceError(
                $"Failed to read from channel '{Name}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    public override async Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default)
    {
        // Two-call pattern across the actor's split output/input pair — per
        // CLAUDE.md's "Console.* Is Banned" rule: write the prompt via the
        // "output" channel (the input channel is typically stdin and is
        // input-only). Falls back to writing via self only when self is
        // bidirectional and no output channel is registered (test fixtures).
        var question = action.Question?.Value;
        if (!string.IsNullOrEmpty(question))
        {
            var output = action.Context?.Actor?.Channels.Resolve(global::app.channel.list.@this.Output);
            if (output != null && output.CanWrite)
            {
                var writeRes = await output.WriteAsync(global::app.data.@this.Ok(question), ct);
                if (!writeRes.Success) return writeRes;
            }
            else if (CanWrite)
            {
                var writeRes = await Write(global::app.data.@this.Ok(question), ct);
                if (!writeRes.Success) return writeRes;
            }
            // No writer at all — proceed to read; the prompt is just lost.
        }

        if (!CanRead)
            return global::app.data.@this.FromError(new ServiceError(
                $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Timeout);

        try
        {
            using var reader = new StreamReader(Stream, ResolveEncoding(),
                detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var line = await reader.ReadLineAsync(timeoutCts.Token);
            // Null from ReadLineAsync = stream EOF. There's no interactive
            // answerer (closed pipe, redirected stdin, non-interactive runner).
            // Fail-fast instead of letting the caller loop on "" forever.
            if (line == null)
                return global::app.data.@this.FromError(new ServiceError(
                    $"Channel '{Name}' has no interactive answerer (stream EOF)",
                    "ChannelEof", 400));
            return global::app.data.@this.Ok(line);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return global::app.data.@this.FromError(new ServiceError(
                $"Channel '{Name}' ask timed out after {Timeout}", "AskTimeout", 408));
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return global::app.data.@this.FromError(new ServiceError(
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
        return ResolveEncoding().GetString(bytes);
    }

    public async Task WriteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var bytes = ResolveEncoding().GetBytes(text);
        await WriteBytesAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Resolves the channel's <see cref="Channel.@this.Encoding"/> name to a real
    /// <see cref="global::System.Text.Encoding"/>. Falls back to UTF-8 when the
    /// property is null/empty or names an unknown encoding.
    /// </summary>
    private global::System.Text.Encoding ResolveEncoding()
    {
        if (string.IsNullOrEmpty(Encoding))
            return global::System.Text.Encoding.UTF8;
        try { return global::System.Text.Encoding.GetEncoding(Encoding); }
        catch (ArgumentException) { return global::System.Text.Encoding.UTF8; }
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
