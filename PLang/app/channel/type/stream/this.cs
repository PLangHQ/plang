using app.error;

namespace app.channel.type.stream;

/// <summary>
/// Concrete Stream-backed channel. Wraps a <see cref="System.IO.Stream"/> for I/O.
/// Extends <see cref="Session.@this"/> — Ask blocks reading from the stream until input arrives.
///
/// Console streams (stdin/stdout/stderr) and arbitrary handed-in streams (HTTP response bodies,
/// memory streams in tests) all use this concrete.
/// </summary>
public sealed class @this : global::app.channel.type.session.@this
{
    private readonly bool _ownsStream;

    /// <summary>The underlying stream this channel reads/writes — a handle, never written with the channel.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
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

    /// <summary>Each text message ends with a newline — console and pipe ergonomics (NDJSON). A stream that
    /// carries one value (a file, a request body) is not framed.</summary>
    [global::app.Debug] public bool Framed { get; init; }

    /// <summary>It writes and reads in the format in play for the writer (<c>%!app.type.format%</c>), not a format of
    /// its own — the console: text for a person at a terminal, plang's own for a program that runs this plang.</summary>
    [global::app.Debug] public bool InPlay { get; init; }

    // the format this channel writes and reads in, for the context at hand
    private global::app.type.kind.@this FormatFor(global::app.actor.context.@this context)
        => InPlay ? context.App.type.FormatOf(context) : context.App.type.list.Mime(Mime.ToString());

    public override bool CanRead => IsOpen && Direction.Value != ChannelDirection.Output && Stream.CanRead;
    public override bool CanWrite => IsOpen && Direction.Value != ChannelDirection.Input && Stream.CanWrite;

    public override async Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
    {
        if (!CanWrite)
            return data.Context.Error(new ServiceError(
                $"Channel '{Name}' does not support writing", "ChannelReadOnly", 400));

        try
        {
            // The channel's Mime is a format, and the format writes the data (a text value on a text channel
            // is its characters, in the channel's encoding).
            var context = Context ?? data.Context;
            // What leaves through the stream is what the value stands for: it is read now, as the writer, at the
            // last moment — the format then decides how it looks.
            var opened = await data.Peek().Open(context);
            if (opened != null) return context.Error(opened);
            var format = FormatFor(context);
            var result = await format.Encode(Stream, data, context, encoding: ResolveEncoding(), ct: ct);
            // A framed channel delimits each text message with a newline — and, following the format in play, each
            // whole Data too: one per line, for the program reading it. Binary content is not framed.
            if (result.Success && Framed && (format.IsText || InPlay))
                await Stream.WriteAsync(ResolveEncoding().GetBytes(System.Environment.NewLine), ct);
            return result;
        }
        // Only the transport fails here as a write error; a program's own error raised while the value renders
        // (a %var% not set) keeps its key and travels to the action.
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
        {
            return data.Context.Error(new ServiceError(
                $"Failed to write to channel '{Name}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    // Line-oriented text mimes are framed one-message-per-line. Binary
    // (image/*, octet-stream) and the self-describing plang envelope are not.
    public override async Task<global::app.data.@this> Read(CancellationToken ct = default)
    {
        if (!CanRead)
            return global::app.data.@this.FromError(new ServiceError(
                $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400));

        try
        {
            // The boundary: read the source bytes and let the base stamp
            // {type, kind} from Mime into lazy Data — no bare text, no eager
            // parse (the value materializes on first touch).
            var bytes = await ReadAllBytesAsync(ct);
            if (InPlay && Context is { } context)
                return await FormatFor(context).Decode(bytes, context, Name, ct: ct);
            return await Read(bytes, ct);
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
        var question = action.Question == null ? null : await action.Question.Value();
        // In plang's own format (the format in play is not text) the ask goes out whole — a pending Ask with its
        // question — and the answer comes back as Data: a program running this plang sees an ask, not a line.
        var format = FormatFor(action.Context);
        var whole = InPlay && !format.IsText;
        if (!string.IsNullOrEmpty(question?.Clr<string>()))
        {
            var output = action.Context?.Actor?.Channel.Get(global::app.channel.list.@this.Output);
            global::app.data.@this asked = whole ? action.Context!.Ok(new module.output.Ask { Question = question }) : action.Context!.Ok(question);
            if (output != null && output.CanWrite)
            {
                var writeRes = await output.WriteAsync(asked, ct);
                if (!writeRes.Success) return writeRes;
            }
            else if (CanWrite)
            {
                var writeRes = await Write(asked, ct);
                if (!writeRes.Success) return writeRes;
            }
            // No writer at all — proceed to read; the prompt is just lost.
        }

        if (!CanRead)
            return action.Context.Error(new ServiceError(
                $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400));

        // A question waits for its answer — no limit of its own. The program's limit (a timeout on the ask) and the
        // run's cancellation arrive through ct.
        try
        {
            using var reader = new StreamReader(Stream, ResolveEncoding(),
                detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var line = await reader.ReadLineAsync(ct);
            // Null from ReadLineAsync = stream EOF. There's no interactive
            // answerer (closed pipe, redirected stdin, non-interactive runner).
            // Fail-fast instead of letting the caller loop on "" forever.
            if (line == null)
                return action.Context.Error(new global::app.error.NoAnswer(
                    $"Channel '{Name}' has no interactive answerer (stream EOF)"));
            // in plang's own format the answer is Data, read in that format; a line typed is text — the user's data itself
            if (whole)
                return await format.Decode(ResolveEncoding().GetBytes(line), action.Context, Name, ct: ct);
            return action.Context.Ok(new global::app.type.item.text.@this(line));
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException
                                                 or OperationCanceledException))
        {
            return action.Context.Error(new ServiceError(
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

    public async Task<string> ReadAllTextAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(cancellationToken);
        return ResolveEncoding().GetString(bytes);
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
