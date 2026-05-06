namespace App.Channels.Channel;

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
/// <see cref="WriteCore"/> / <see cref="ReadCore"/> / <see cref="AskCore"/>.
///
/// The split into Session / Message gives external developers two structural bases
/// to extend — they pick the one matching their transport's nature:
///   - <see cref="Session.@this"/>: kept-open connection. Ask blocks until answer arrives.
///   - <see cref="Message.@this"/>: one-shot exchange. Ask returns Suspend; resume via callback.
/// </summary>
public abstract class @this : IAsyncDisposable, IDisposable
{
    /// <summary>Logical channel name (e.g. "output", "logger"). Case-insensitive at registry level.</summary>
    public string Name { get; init; } = "";

    /// <summary>Logical role within the actor's I/O surface. Custom-named channels use Role.None.</summary>
    public global::App.Channels.Channel.Role.@this Role { get; init; } = global::App.Channels.Channel.Role.@this.None;

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

    /// <summary>Free-form metadata bag (compatibility with v1 Channel surface).</summary>
    public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether reading is supported. Default tracks Direction + IsOpen; concretes can override.
    /// </summary>
    public virtual bool CanRead => IsOpen && Direction != ChannelDirection.Output;

    /// <summary>
    /// Whether writing is supported. Default tracks Direction + IsOpen; concretes can override.
    /// </summary>
    public virtual bool CanWrite => IsOpen && Direction != ChannelDirection.Input;

    /// <summary>
    /// Core write — concrete subtypes implement. Receives the full Data envelope (Rule 7,
    /// relay don't repackage); the channel's serializer decides how to render.
    /// </summary>
    public abstract Task<Data.@this> WriteCore(Data.@this data, CancellationToken ct = default);

    /// <summary>
    /// Core read — concrete subtypes implement.
    /// </summary>
    public abstract Task<Data.@this> ReadCore(CancellationToken ct = default);

    /// <summary>
    /// Core ask — concrete subtypes implement. Session blocks until answer; Message returns Suspend.
    /// </summary>
    public abstract Task<Data.@this> AskCore(Data.@this prompt, CancellationToken ct = default);

    /// <summary>
    /// Public write entry. Stage 8 wraps in event firing; for now relays directly.
    /// </summary>
    public virtual Task<Data.@this> WriteAsync(Data.@this data, CancellationToken ct = default)
        => WriteCore(data, ct);

    /// <summary>
    /// Public read entry. Stage 8 wraps in event firing; for now relays directly.
    /// </summary>
    public virtual Task<Data.@this> ReadAsync(CancellationToken ct = default)
        => ReadCore(ct);

    /// <summary>
    /// Public ask entry. Stage 8 wraps in event firing; for now relays directly.
    /// </summary>
    public virtual Task<Data.@this> Ask(Data.@this prompt, CancellationToken ct = default)
        => AskCore(prompt, ct);

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
