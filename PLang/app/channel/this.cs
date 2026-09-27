namespace app.channel;

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
/// Abstract base for every channel in PLang.
/// Concrete subtypes (<see cref="Stream.@this"/>, <see cref="Goal.@this"/>) implement
/// <see cref="Write"/> / <see cref="Read"/> / <see cref="Ask"/>.
///
/// The split into Session / Message gives external developers two structural bases
/// to extend — they pick the one matching their transport's nature:
///   - <see cref="Session.@this"/>: kept-open connection. Ask blocks until answer arrives.
///   - <see cref="Message.@this"/>: one-shot exchange. Ask returns Suspend; resume via callback.
/// </summary>
[global::app.Attributes.PlangType("channel")]
public abstract class @this : global::app.type.item.@this, IAsyncDisposable, IDisposable
{
    /// <summary>A structure — written through the reflection kind, its [Out]/[Debug] members.</summary>
    public override bool IsLeaf => false;

    public override ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
        => new global::app.type.item.kind.reflection.@this().Output(this, writer, mode, context);

    /// <summary>A channel is a live resource: a copy of it is it.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>A channel writes, reads and asks through the channel type's events, then its own.</summary>
    protected internal override global::app.type.item.@this? Level(int depth, global::app.actor.context.@this context) => depth switch
    {
        0 => context.App.channel,
        1 => this,
        _ => null,
    };

    /// <summary>Logical channel name (e.g. "output", "logger"). Case-insensitive at registry level.</summary>
    public string Name { get; init; } = "";

    /// <summary>Direction (Input / Output / Bidirectional).</summary>
    public ChannelDirection Direction { get; init; } = ChannelDirection.Bidirectional;

    /// <summary>Buffer size in bytes. Stream-backed channels honour; Goal channel ignores. Default 4096.</summary>
    public long Buffer { get; init; } = 4096;

    /// <summary>I/O timeout. JSON wire shape is ISO 8601 (e.g. "PT30S") via custom converter. Default 30s.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>MIME type that drives serializer selection. Default "text/plain".</summary>
    public string Mime { get; init; } = "text/plain";

    /// <summary>Text encoding name. Default "utf-8".</summary>
    public string Encoding { get; init; } = "utf-8";

    /// <summary>Optional encryption provider reference. Null = no encryption.</summary>
    public string? Encryption { get; init; }

    /// <summary>Signing provider reference. Default "auto" — System identity at write time.</summary>
    public string? Signing { get; init; } = "auto";

    /// <summary>Whether the channel is currently open. Concrete subtypes manage.</summary>
    public bool IsOpen { get; protected set; } = true;

    /// <summary>UTC timestamp of construction.</summary>
    public DateTime Created { get; } = DateTime.UtcNow;

    /// <summary>
    /// The actor this channel belongs to — set by the Channels collection on Register. Its context is the one
    /// the channel's events fire in.
    /// </summary>
    public global::app.actor.@this Actor { get; internal set; } = null!;

    /// <summary>
    /// The Channels collection this channel belongs to — set by
    /// <see cref="app.channel.list.@this.Register"/>; the channel's events fire in its context.
    /// </summary>
    public global::app.channel.list.@this Channels { get; internal set; } = null!;

    /// <summary>
    /// Whether reading is supported. Default tracks Direction + IsOpen; concretes can override.
    /// </summary>
    public virtual bool CanRead => IsOpen && Direction != ChannelDirection.Output;

    /// <summary>
    /// Whether writing is supported. Default tracks Direction + IsOpen; concretes can override.
    /// </summary>
    public virtual bool CanWrite => IsOpen && Direction != ChannelDirection.Input;

    /// <summary>
    /// Abstract write — concrete subtypes implement. Receives the full Data (Rule 7,
    /// relay don't repackage); the channel's serializer decides how to render.
    /// </summary>
    public abstract Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default);

    /// <summary>
    /// Abstract read — concrete subtypes implement.
    /// </summary>
    public abstract Task<global::app.data.@this> Read(CancellationToken ct = default);

    /// <summary>
    /// Abstract ask — concrete subtypes implement. Takes the action directly so the
    /// channel can extract <c>Question.Value</c>, capture a Snapshot, or read
    /// other action state. Session blocks until answer; Message returns
    /// <c>Data&lt;Ask&gt;</c> with Snapshot attached (engine short-circuits).
    /// </summary>
    public abstract Task<global::app.data.@this> Ask(module.action.output.ask action, CancellationToken ct = default);

    // The context the channel's events fire in — its actor's, else its list's app's system context (a
    // Service-owned list has no actor); null for a channel that belongs to no list.
    private protected global::app.actor.context.@this? Context => Actor?.Context ?? Channels?.App.System.Context;

    /// <summary>Whether the channel takes writes now — a list's <c>Get</c> answers only a channel that does.
    /// A goal channel doesn't while its own goal body runs, so a body writing to its own name can't loop.</summary>
    public virtual bool Available => true;

    /// <summary>Writes <paramref name="text"/> — a write of that text through <see cref="WriteAsync"/>, so it fires
    /// <c>on.write</c> as any write does. The channel frames it (a text channel ends the line).</summary>
    public Task<global::app.data.@this> WriteText(string text, CancellationToken ct = default)
        => WriteAsync((Context ?? throw new InvalidOperationException(
            $"channel '{Name}' belongs to no list — it has no context to write text in")).Ok(text), ct);

    /// <summary>
    /// Public write entry, through the channel's <c>on.write</c>: what is bound before it is handed the data —
    /// a failure or a Handled answer is the write's answer and nothing is written; else the write, whose own
    /// transport failure (the .NET I/O boundary) is an error result; then what is bound after it runs on the
    /// result either way.
    /// </summary>
    public virtual async Task<global::app.data.@this> WriteAsync(global::app.data.@this data, CancellationToken ct = default)
    {
        var context = Context ?? data.Context;
        var answer = await on.write.Before(this, context, data);
        global::app.data.@this result;
        if (answer is { Success: false } or { Handled: true }) result = Refused(answer);
        else
        {
            try { result = await Write(data, ct); }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                result = data.Context.Error(new global::app.error.ServiceError(
                    $"Channel '{Name}' write failed: {ex.Message}", "WriteError") { Exception = ex });
            }
        }
        return await on.write.After(this, result, context);
    }

    /// <summary>
    /// Public read entry, through the channel's <c>on.read</c> — as <see cref="WriteAsync"/>. A channel that
    /// belongs to no list has no context to fire in, and reads plainly.
    /// </summary>
    public virtual async Task<global::app.data.@this> ReadAsync(CancellationToken ct = default)
    {
        var context = Context;
        var answer = context != null ? await on.read.Before(this, context) : null;
        global::app.data.@this result;
        if (answer is { Success: false } or { Handled: true }) result = Refused(answer);
        else
        {
            try { result = await Read(ct); }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                result = global::app.data.@this.FromError(new global::app.error.ServiceError(
                    $"Channel '{Name}' read failed: {ex.Message}", "ReadError") { Exception = ex });
            }
        }
        return context != null ? await on.read.After(this, result, context) : result;
    }

    /// <summary>
    /// Public ask entry, through the channel's <c>on.ask</c> — as <see cref="WriteAsync"/>, in the asking
    /// action's context. After fires when the ask completes (Session: post-answer; Message: pre-suspend — the
    /// channel kind decides timing).
    /// </summary>
    public virtual async Task<global::app.data.@this> AskAsync(module.action.output.ask action, CancellationToken ct = default)
    {
        var context = action.Context;
        var answer = await on.ask.Before(this, context);
        global::app.data.@this result;
        if (answer is { Success: false } or { Handled: true }) result = Refused(answer);
        else
        {
            try { result = await Ask(action, ct); }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                result = action.Context.Error(new global::app.error.ServiceError(
                    $"Channel '{Name}' ask failed: {ex.Message}", "AskError") { Exception = ex });
            }
        }
        return await on.ask.After(this, result, context);
    }

    // What a before-binding answered in place of the channel's own work — the operation's answer. A cancel is
    // spent here: the answer goes on as a plain result, so a step reading it doesn't take it for a stop.
    private global::app.data.@this Refused(global::app.data.@this answer)
    {
        answer.Handled = false;
        return answer;
    }

    /// <summary>
    /// The one read boundary. A concrete kind reads its source bytes and hands them here; the channel's
    /// <see cref="Mime"/> is a format, and the format decodes them (<c>kind.Decode</c>) — for a value's format
    /// into <em>lazy</em> Data that materializes on first touch, never at read time, so couriers relay a
    /// value without forcing a parse; for plang's own format (<c>application/plang</c>) into the whole Data
    /// the bytes are. No format is special-cased here.
    /// </summary>
    protected Task<global::app.data.@this> Read(byte[] raw, CancellationToken ct = default)
    {
        var context = Context ?? throw new InvalidOperationException(
            $"channel '{Name}' belongs to no list — it has no context to read in");
        return context.App.type.list.Mime(Mime ?? "", context).kind.Decode(raw, context, Name, ct: ct);
    }

    /// <summary>
    /// Resolves the channel's <see cref="Encoding"/> name to a real
    /// <see cref="global::System.Text.Encoding"/> — UTF-8 when none is named. An encoding name no encoding
    /// answers to is the channel misconfigured: an error naming it, never a quiet UTF-8. Owned by the base so
    /// every concrete kind decodes the same way.
    /// </summary>
    protected global::System.Text.Encoding ResolveEncoding()
    {
        if (string.IsNullOrEmpty(Encoding))
            return global::System.Text.Encoding.UTF8;
        try { return global::System.Text.Encoding.GetEncoding(Encoding); }
        catch (System.ArgumentException ex)
        {
            throw new InvalidOperationException($"channel '{Name}' names the encoding '{Encoding}', which is no encoding", ex);
        }
    }

    /// <summary>Closes the channel and any owned resources.</summary>
    public virtual void Close()
    {
        IsOpen = false;
    }

    public virtual void Dispose() => Close();

    public virtual ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }
}
