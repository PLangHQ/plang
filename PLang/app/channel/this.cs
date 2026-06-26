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
    /// Bindings fire from WriteAsync / ReadAsync / AskAsync: BeforeWrite, AfterWrite,
    /// BeforeRead, AfterRead, OnAsk.
    /// </summary>
    public global::app.channel.@event.@this Events { get; } = new();

    /// <summary>
    /// The actor this channel belongs to — set by the Channels collection on
    /// Register. Channel-event firing reads from <c>Actor.Context.Events</c>
    /// (the same place <c>event.on</c> writes to). Channels live per-actor;
    /// the actor is the natural scope for channel-bound bindings.
    /// </summary>
    public global::app.actor.@this Actor { get; internal set; } = null!;

    /// <summary>
    /// The Channels collection this channel belongs to — set by
    /// <see cref="app.channel.list.@this.Register"/>. Stream channels navigate through
    /// this to reach their parent Channels' Serializers registry; the per-actor
    /// Channels owns the single Serializers home.
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
    public abstract Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default);

    /// <summary>
    /// Public write entry. Wraps Write in BeforeWrite / AfterWrite event firing.
    /// Before-handler throwing aborts the write (returns Data.Error,
    /// AfterWrite is suppressed). After-handler always fires when Write was
    /// reached (success or error); their throws are suppressed.
    /// </summary>
    public virtual async Task<global::app.data.@this> WriteAsync(global::app.data.@this data, CancellationToken ct = default)
    {
        // Fire BeforeWrite — abort on throw.
        var beforeAborted = await FireBefore(global::app.@event.Trigger.BeforeWrite, data);
        if (beforeAborted != null) return beforeAborted;

        global::app.data.@this result;
        try { result = await Write(data, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = data.Context.Error(new global::app.error.ServiceError(
                $"Channel '{Name}' write failed: {ex.Message}", "WriteError") { Exception = ex });
        }

        // Fire AfterWrite — always (even on error). Handler throws are suppressed.
        await FireAfter(global::app.@event.Trigger.AfterWrite, result);
        return result;
    }

    /// <summary>
    /// Public read entry. Wraps Read in BeforeRead / AfterRead event firing.
    /// </summary>
    public virtual async Task<global::app.data.@this> ReadAsync(CancellationToken ct = default)
    {
        var beforeAborted = await FireBefore(global::app.@event.Trigger.BeforeRead, global::app.data.@this.Ok());
        if (beforeAborted != null) return beforeAborted;

        global::app.data.@this result;
        try { result = await Read(ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = global::app.data.@this.FromError(new global::app.error.ServiceError(
                $"Channel '{Name}' read failed: {ex.Message}", "ReadError") { Exception = ex });
        }

        await FireAfter(global::app.@event.Trigger.AfterRead, result);
        return result;
    }

    /// <summary>
    /// Public ask entry. Fires OnAsk after the Ask completes (Session: post-answer;
    /// Message: pre-suspend — the channel kind decides timing).
    /// </summary>
    public virtual async Task<global::app.data.@this> AskAsync(module.output.ask action, CancellationToken ct = default)
    {
        global::app.data.@this result;
        try { result = await Ask(action, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = action.Context.Error(new global::app.error.ServiceError(
                $"Channel '{Name}' ask failed: {ex.Message}", "AskError") { Exception = ex });
        }

        await FireAfter(global::app.@event.Trigger.OnAsk, result);
        return result;
    }

    /// <summary>
    /// Bindings that match this channel for the given event type. Sources:
    /// per-channel Events list, plus app-level bindings whose ChannelName equals
    /// this channel's name. Per-channel bindings precede app-level (registration order).
    /// </summary>
    private IEnumerable<global::app.@event.lifecycle.binding.@this> MatchingBindings(global::app.@event.Trigger type)
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
            foreach (var b in app.Event.GetBindings(type))
                if (string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase))
                    yield return b;
        }
    }

    private async Task<global::app.data.@this?> FireBefore(global::app.@event.Trigger type, global::app.data.@this data)
    {
        foreach (var binding in MatchingBindings(type))
        {
            if (Events.IsActive(binding.Id)) continue;   // recursion guard
            using var _ = Events.Enter(binding.Id);
            try
            {
                var result = await InvokeChannelHandler(binding, data);
                if (!result.Success) return result;
            }
            catch (Exception ex)
            {
                return data.Context.Error(new global::app.error.ServiceError(
                    $"Channel event handler for {type} on '{Name}' threw: {ex.Message}",
                    "ChannelEventAborted") { Exception = ex });
            }
        }
        return null;
    }

    private async Task FireAfter(global::app.@event.Trigger type, global::app.data.@this data)
    {
        foreach (var binding in MatchingBindings(type))
        {
            if (Events.IsActive(binding.Id)) continue;
            using var _ = Events.Enter(binding.Id);
            try { await InvokeChannelHandler(binding, data); }
            catch { /* After-handler throws are suppressed — original outcome stands. */ }
        }
    }

    private Task<global::app.data.@this> InvokeChannelHandler(
        global::app.@event.lifecycle.binding.@this binding,
        global::app.data.@this data)
    {
        // Bindings receive (context, action=null, result=data). The context comes
        // from the channel's owning Actor when the channel went through
        // Channels.Register; tests sometimes construct a channel directly without
        // an Actor. Most handlers don't read context (they capture what they need at
        // registration), so we forward null rather than skip — handlers that *do*
        // need context (notably the one event.on installs to dispatch a goal) can
        // guard locally. The diagnostic surfaces the case so production paths
        // can be spotted in --debug output.
        var context = Actor?.Context;
        if (context == null)
            _ = Channels?.App?.Debug?.Write($"[Channel '{Name}'] binding {binding.Id} firing with no Actor — handlers receive null context");
        return binding.Handler(context!, null, data);
    }

    /// <summary>
    /// The one read boundary. A concrete kind reads its source bytes and hands
    /// them here; the channel's <see cref="Mime"/> decides the value's
    /// <c>{type, kind}</c> and the result is <em>lazy</em> Data — the value
    /// materializes on first touch through the reader registry, never at read
    /// time. Couriers (variable memory, callstack, routing) thus relay a value
    /// without forcing a parse (the OBP courier rule holds by construction).
    ///
    /// <para>When the Mime resolves to the plang <em>transport</em> serializer,
    /// the bytes are the self-describing Data container, not a value — the
    /// serializer reconstructs the Data (whose own value slot stays lazy via
    /// <c>Wire.Read</c>). Any other Mime names a value: the bytes are stamped
    /// <c>{type, kind}</c> and held as the raw source form. The container is
    /// recognised by <em>which serializer owns the Mime</em>, not by a name
    /// match — so value MIMEs that merely share the <c>application/plang</c>
    /// prefix (e.g. <c>application/plang-goal</c>, a goal source) correctly
    /// stamp as values, with no per-extension special-casing anywhere.</para>
    /// </summary>
    protected async Task<global::app.data.@this> StampReadAsync(byte[] raw, CancellationToken ct = default)
    {
        // The container is whatever the plang transport serializer is registered
        // for — a semantic check, not a string prefix. Sibling MIMEs that aren't
        // the container (application/plang-goal, application/json, …) fall through
        // to value stamping.
        if (Channels?.Serializers.GetByType(Mime ?? "") is global::app.channel.serializer.plang.@this serializer)
        {
            using var ms = new MemoryStream(raw);
            // The container deserializer returns the reconstructed Data itself
            // (never an envelope around it — the store seam rejects bare nesting).
            return await serializer.DeserializeAsync(ms, cancellationToken: ct);
        }
        return StampValue(raw);
    }

    /// <summary>
    /// Stamps a value-typed payload — <see cref="Mime"/> names a value, not the
    /// plang container. Content off I/O is raw bytes: the source holds the
    /// <c>byte[]</c> typed <c>binary</c> + the kind, and the kind's reader decodes
    /// on access (json→dict, jpg→image, md→text). No eager bytes-vs-string split.
    /// </summary>
    private global::app.data.@this StampValue(byte[] raw)
    {
        var context = Actor?.Context;
        return global::app.data.@this.FromRaw(raw, StampType(context), context, Name);
    }

    /// <summary>
    /// The <c>{binary, kind}</c> the channel's <see cref="Mime"/> stamps. Content
    /// off I/O is raw bytes — it IS binary; the mime's subtype is the kind (the
    /// decode hint). octet-stream / unset Mime → <c>binary</c> with no kind.
    /// Everything else routes through <see cref="app.format.list.@this.TypeFromMime"/>.
    /// </summary>
    private global::app.type.@this StampType(global::app.actor.context.@this? context)
    {
        var t = Channels?.App?.Format?.TypeFromMime(Mime ?? "")
                ?? global::app.type.@this.Create("binary", null, context: context);
        t.Context = context;
        return t;
    }

    /// <summary>
    /// Resolves the channel's <see cref="Encoding"/> name to a real
    /// <see cref="global::System.Text.Encoding"/>. Falls back to UTF-8 when the
    /// property is null/empty or names an unknown encoding. Owned by the base so
    /// every concrete kind decodes the same way.
    /// </summary>
    protected global::System.Text.Encoding ResolveEncoding()
    {
        if (string.IsNullOrEmpty(Encoding))
            return global::System.Text.Encoding.UTF8;
        try { return global::System.Text.Encoding.GetEncoding(Encoding); }
        catch (System.ArgumentException) { return global::System.Text.Encoding.UTF8; }
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
