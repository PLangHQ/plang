namespace app.channels.channel;

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
    /// Channel-event bindings (Stage 8) with their lock discipline and recursion
    /// guard encapsulated. Same shape spirit as Goal.Events / Step.Events.
    /// Bindings fire from WriteAsync / ReadAsync / Ask: BeforeWrite, AfterWrite,
    /// BeforeRead, AfterRead, OnAsk.
    /// </summary>
    public global::app.channels.channel.Events.@this Events { get; } = new();

    /// <summary>
    /// The actor this channel belongs to — set by the Channels collection on
    /// Register. Channel-event firing reads from <c>Actor.Context.Events</c>
    /// (the same place <c>event.on</c> writes to). Channels live per-actor;
    /// the actor is the natural scope for channel-bound bindings.
    /// </summary>
    public global::app.actor.@this? Actor { get; internal set; }

    /// <summary>
    /// The Channels collection this channel belongs to — set by
    /// <see cref="app.channels.@this.Register"/>. Stream channels navigate through
    /// this to reach their parent Channels' Serializers registry; the per-actor
    /// Channels owns the single Serializers home.
    /// </summary>
    public global::app.channels.@this? Channels { get; internal set; }

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
    public abstract Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default);

    /// <summary>
    /// Core read — concrete subtypes implement.
    /// </summary>
    public abstract Task<global::app.data.@this> ReadCore(CancellationToken ct = default);

    /// <summary>
    /// Core ask — concrete subtypes implement. Session blocks until answer; Message returns Suspend.
    /// </summary>
    public abstract Task<global::app.data.@this> AskCore(global::app.data.@this prompt, CancellationToken ct = default);

    /// <summary>
    /// Public write entry. Wraps WriteCore in BeforeWrite / AfterWrite event firing.
    /// Before-handler throwing aborts the write (returns Data.Error,
    /// AfterWrite is suppressed). After-handler always fires when WriteCore was
    /// reached (success or error); their throws are suppressed.
    /// </summary>
    public virtual async Task<global::app.data.@this> WriteAsync(global::app.data.@this data, CancellationToken ct = default)
    {
        // Fire BeforeWrite — abort on throw.
        var beforeAborted = await FireBefore(global::app.events.EventType.BeforeWrite, data, null);
        if (beforeAborted != null) return beforeAborted;

        global::app.data.@this result;
        try { result = await WriteCore(data, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = global::app.data.@this.FromError(new global::app.errors.ServiceError(
                $"Channel '{Name}' write failed: {ex.Message}", "WriteError") { Exception = ex });
        }

        // Fire AfterWrite — always (even on error). Handler throws are suppressed.
        await FireAfter(global::app.events.EventType.AfterWrite, result, null);
        return result;
    }

    /// <summary>
    /// Public read entry. Wraps ReadCore in BeforeRead / AfterRead event firing.
    /// </summary>
    public virtual async Task<global::app.data.@this> ReadAsync(CancellationToken ct = default)
    {
        var beforeAborted = await FireBefore(global::app.events.EventType.BeforeRead, global::app.data.@this.Ok(), null);
        if (beforeAborted != null) return beforeAborted;

        global::app.data.@this result;
        try { result = await ReadCore(ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = global::app.data.@this.FromError(new global::app.errors.ServiceError(
                $"Channel '{Name}' read failed: {ex.Message}", "ReadError") { Exception = ex });
        }

        await FireAfter(global::app.events.EventType.AfterRead, result, null);
        return result;
    }

    /// <summary>
    /// Public ask entry. Fires OnAsk after the Core completes (Session: post-answer;
    /// Message: pre-suspend — the channel kind decides timing).
    /// </summary>
    public virtual async Task<global::app.data.@this> Ask(global::app.data.@this prompt, CancellationToken ct = default)
    {
        global::app.data.@this result;
        try { result = await AskCore(prompt, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = global::app.data.@this.FromError(new global::app.errors.ServiceError(
                $"Channel '{Name}' ask failed: {ex.Message}", "AskError") { Exception = ex });
        }

        await FireAfter(global::app.events.EventType.OnAsk, result, null);
        return result;
    }

    /// <summary>
    /// Bindings that match this channel for the given event type. Sources:
    /// per-channel Events list, plus app-level bindings whose ChannelName equals
    /// this channel's name. Per-channel bindings precede app-level (registration order).
    /// </summary>
    private IEnumerable<global::app.events.lifecycle.bindings.binding.@this> MatchingBindings(global::app.events.EventType type)
    {
        // Per-channel bindings (with their own lock + filter, owned by Events).
        foreach (var b in Events.Match(type, Name)) yield return b;

        // Per-actor lifecycle events — where event.on writes its bindings.
        if (Actor != null)
        {
            foreach (var b in Actor.Context.Events.GetBindings(type))
                if (string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase))
                    yield return b;
        }

        // App-level bindings — match across actors so one binding can cover
        // every channel-of-name "logger" regardless of which actor owns it.
        // Navigation: Channels (parent collection) → App. Service-owned
        // Channels have no Actor, so we navigate through Channels.App directly.
        if (Channels?.App is { } app)
        {
            foreach (var b in app.Events.GetBindings(type))
                if (string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase))
                    yield return b;
        }
    }

    private async Task<global::app.data.@this?> FireBefore(global::app.events.EventType type, global::app.data.@this data, Callback.AskCallback? ask)
    {
        foreach (var binding in MatchingBindings(type))
        {
            if (Events.IsActive(binding.Id)) continue;   // recursion guard
            using var _ = Events.Enter(binding.Id);
            try
            {
                var result = await InvokeChannelHandler(binding, data, ask);
                if (!result.Success) return result;
            }
            catch (Exception ex)
            {
                return global::app.data.@this.FromError(new global::app.errors.ServiceError(
                    $"Channel event handler for {type} on '{Name}' threw: {ex.Message}",
                    "ChannelEventAborted") { Exception = ex });
            }
        }
        return null;
    }

    private async Task FireAfter(global::app.events.EventType type, global::app.data.@this data, Callback.AskCallback? ask)
    {
        foreach (var binding in MatchingBindings(type))
        {
            if (Events.IsActive(binding.Id)) continue;
            using var _ = Events.Enter(binding.Id);
            try { await InvokeChannelHandler(binding, data, ask); }
            catch { /* After-handler throws are suppressed — original outcome stands. */ }
        }
    }

    private Task<global::app.data.@this> InvokeChannelHandler(
        global::app.events.lifecycle.bindings.binding.@this binding,
        global::app.data.@this data,
        Callback.AskCallback? ask)
    {
        // Bindings receive (context, action=null, result=data). The context comes
        // from the channel's owning Actor when the channel went through
        // Channels.Register; tests sometimes construct a channel directly without
        // an Actor. Most handlers don't read ctx (they capture what they need at
        // registration), so we forward null rather than skip — handlers that *do*
        // need ctx (notably the one event.on installs to dispatch a goal) can
        // guard locally. The diagnostic surfaces the case so production paths
        // can be spotted in --debug output.
        var ctx = Actor?.Context;
        if (ctx == null)
            _ = Channels?.App?.Debug?.Write($"[Channel '{Name}'] binding {binding.Id} firing with no Actor — handlers receive null ctx");
        return binding.Handler(ctx!, null, data);
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
