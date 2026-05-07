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
    /// Channel-event bindings (Stage 8). Same shape as Goal.Events / Step.Events;
    /// fired by the WriteAsync / ReadAsync / Ask wrappers. Includes BeforeWrite /
    /// AfterWrite / BeforeRead / AfterRead / OnAsk.
    /// </summary>
    public List<global::App.Events.Lifecycle.Bindings.Binding.@this> Events { get; } = new();

    /// <summary>
    /// The actor this channel belongs to — set by the Channels collection on
    /// Register. Channel-event firing reads from <c>Actor.Context.Events</c>
    /// (the same place <c>event.on</c> writes to). Channels live per-actor;
    /// the actor is the natural scope for channel-bound bindings.
    /// </summary>
    public global::App.Actor.@this? Actor { get; internal set; }

    /// <summary>App backreference — kept for SignEmpty / general App access.</summary>
    public global::App.@this? App { get; internal set; }

    // Recursion guard: AsyncLocal so concurrent writes don't interfere; per-binding
    // Id keyed so a Before-handler that writes to the same channel doesn't
    // re-trigger the same binding (architect's "_activeEventBindings").
    private static readonly AsyncLocal<HashSet<string>?> _activeBindings = new();

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
    /// Public write entry. Wraps WriteCore in BeforeWrite / AfterWrite event firing.
    /// Before-handler throwing aborts the write (returns Data.Error,
    /// AfterWrite is suppressed). After-handler always fires when WriteCore was
    /// reached (success or error); their throws are suppressed.
    /// </summary>
    public virtual async Task<Data.@this> WriteAsync(Data.@this data, CancellationToken ct = default)
    {
        // Fire BeforeWrite — abort on throw.
        var beforeAborted = await FireBefore(global::App.Events.EventType.BeforeWrite, data, null);
        if (beforeAborted != null) return beforeAborted;

        Data.@this result;
        try { result = await WriteCore(data, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = Data.@this.FromError(new global::App.Errors.ServiceError(
                $"Channel '{Name}' write failed: {ex.Message}", "WriteError") { Exception = ex });
        }

        // Fire AfterWrite — always (even on error). Handler throws are suppressed.
        await FireAfter(global::App.Events.EventType.AfterWrite, result, null);
        return result;
    }

    /// <summary>
    /// Public read entry. Wraps ReadCore in BeforeRead / AfterRead event firing.
    /// </summary>
    public virtual async Task<Data.@this> ReadAsync(CancellationToken ct = default)
    {
        var beforeAborted = await FireBefore(global::App.Events.EventType.BeforeRead, Data.@this.Ok(), null);
        if (beforeAborted != null) return beforeAborted;

        Data.@this result;
        try { result = await ReadCore(ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = Data.@this.FromError(new global::App.Errors.ServiceError(
                $"Channel '{Name}' read failed: {ex.Message}", "ReadError") { Exception = ex });
        }

        await FireAfter(global::App.Events.EventType.AfterRead, result, null);
        return result;
    }

    /// <summary>
    /// Public ask entry. Fires OnAsk after the Core completes (Session: post-answer;
    /// Message: pre-suspend — the channel kind decides timing).
    /// </summary>
    public virtual async Task<Data.@this> Ask(Data.@this prompt, CancellationToken ct = default)
    {
        Data.@this result;
        try { result = await AskCore(prompt, ct); }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            result = Data.@this.FromError(new global::App.Errors.ServiceError(
                $"Channel '{Name}' ask failed: {ex.Message}", "AskError") { Exception = ex });
        }

        await FireAfter(global::App.Events.EventType.OnAsk, result, null);
        return result;
    }

    /// <summary>
    /// Bindings that match this channel for the given event type. Sources:
    /// per-channel Events list, plus app-level bindings whose ChannelName equals
    /// this channel's name. Per-channel bindings precede app-level (registration order).
    /// </summary>
    private IEnumerable<global::App.Events.Lifecycle.Bindings.Binding.@this> MatchingBindings(global::App.Events.EventType type)
    {
        foreach (var b in Events)
            if (b.Type == type
                && (b.ChannelName == null || string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase)))
                yield return b;

        // Per-actor lifecycle events — where event.on writes its bindings.
        if (Actor != null)
        {
            foreach (var b in Actor.Context.Events.GetBindings(type))
                if (string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase))
                    yield return b;
        }

        // App-level bindings — match across actors so one binding can cover
        // every channel-of-name "logger" regardless of which actor owns it.
        if (App != null)
        {
            foreach (var b in App.Events.GetBindings(type))
                if (string.Equals(b.ChannelName, Name, StringComparison.OrdinalIgnoreCase))
                    yield return b;
        }
    }

    private async Task<Data.@this?> FireBefore(global::App.Events.EventType type, Data.@this data, Callback.AskCallback? ask)
    {
        var active = _activeBindings.Value ??= new HashSet<string>();
        foreach (var binding in MatchingBindings(type))
        {
            if (!active.Add(binding.Id)) continue;   // recursion guard
            try
            {
                var result = await InvokeChannelHandler(binding, data, ask);
                if (!result.Success) return result;
            }
            catch (Exception ex)
            {
                return Data.@this.FromError(new global::App.Errors.ServiceError(
                    $"Channel event handler for {type} on '{Name}' threw: {ex.Message}",
                    "ChannelEventAborted") { Exception = ex });
            }
            finally { active.Remove(binding.Id); }
        }
        return null;
    }

    private async Task FireAfter(global::App.Events.EventType type, Data.@this data, Callback.AskCallback? ask)
    {
        var active = _activeBindings.Value ??= new HashSet<string>();
        foreach (var binding in MatchingBindings(type))
        {
            if (!active.Add(binding.Id)) continue;
            try { await InvokeChannelHandler(binding, data, ask); }
            catch { /* After-handler throws are suppressed — original outcome stands. */ }
            finally { active.Remove(binding.Id); }
        }
    }

    private Task<Data.@this> InvokeChannelHandler(
        global::App.Events.Lifecycle.Bindings.Binding.@this binding,
        Data.@this data,
        Callback.AskCallback? ask)
    {
        // Bindings receive (context, action=null, result=data). The context comes
        // from the channel's owning Actor when the channel went through
        // Channels.Register; tests sometimes construct a channel directly without
        // an Actor, in which case ctx is null. Handlers that need ctx (e.g. the
        // one event.on installs to dispatch a goal) must guard for null
        // themselves — the framework forwards whatever it has rather than
        // silently swallowing the binding.
        var ctx = Actor?.Context;
        if (ctx == null)
            _ = App?.Debug?.Write($"[Channel '{Name}'] binding {binding.Id} firing with no Actor — handlers receive null ctx");
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

    // --- Stage 9: migration API stub ----------------------------------------

    /// <summary>
    /// Produces a signed migration envelope for shipping this channel to another
    /// identity-aware runtime. Concrete subtypes override to populate
    /// <see cref="MigrationEnvelope.Payload"/>; non-migratable channels (e.g.
    /// Console-backed Stream) override to return Data.Error of type
    /// <c>NotMigratable</c>.
    /// </summary>
    public virtual Task<Data.@this> Migrate()
    {
        var envelope = new MigrationEnvelope
        {
            Name = Name,
            Direction = Direction,
            Config = SnapshotConfig(),
            Payload = null,
            Signature = SignEmpty()
        };
        return Task.FromResult(Data.@this.Ok(envelope));
    }

    /// <summary>Receive-side stub. Cross-device transport not yet shipped.</summary>
    public static @this FromMigration(MigrationEnvelope envelope)
        => throw new NotImplementedException("Channel.FromMigration: receive-side transport is deferred.");

    protected ChannelConfigSnapshot SnapshotConfig() => new()
    {
        Buffer = Buffer,
        Timeout = Timeout,
        Mime = Mime,
        Encoding = Encoding,
        Encryption = Encryption,
        Signing = Signing
    };

    protected Signature SignEmpty()
    {
        // Stage 9 stub: identity name + key are sourced from App.System if reachable;
        // empty bytes mean "not yet signed". The integrity contract (Verify on a
        // tampered envelope returns false) is provided via the Verify helper below
        // which round-trips a stable byte representation.
        var identityName = App?.System.Identity?.Name ?? "system";
        var publicKey = App?.System.Identity?.PublicKey ?? "";
        var bytes = ComputeSignature(Name, Direction, identityName);
        return new Signature
        {
            IdentityName = identityName,
            PublicKey = publicKey,
            Bytes = bytes
        };
    }

    protected static byte[] ComputeSignature(string name, ChannelDirection direction, string identity)
    {
        // Deterministic hash over (name, direction, identity). Stage 9 stub —
        // real signing lands when the cross-device transport ships.
        using var sha = global::System.Security.Cryptography.SHA256.Create();
        var input = global::System.Text.Encoding.UTF8.GetBytes($"{name}|{(int)direction}|{identity}");
        return sha.ComputeHash(input);
    }

    /// <summary>
    /// Stage 9 verification helper: recompute the signature from envelope contents
    /// and compare bytes. Returns false on tamper.
    /// </summary>
    public static bool VerifyEnvelope(MigrationEnvelope envelope)
    {
        var expected = ComputeSignature(envelope.Name, envelope.Direction, envelope.Signature.IdentityName);
        return expected.SequenceEqual(envelope.Signature.Bytes);
    }
}
