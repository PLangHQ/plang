using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace app.channel.serializer.plang;

/// <summary>
/// The canonical PLang-to-PLang transport serializer (<c>application/plang</c>).
///
/// <para>
/// Composes its STJ options from a fresh base (camelCase + null-skip), then adds
/// the path converter (Context-bound when available), the
/// <see cref="global::app.data.Wire"/> (sign-if-missing during the
/// walk + canonical four-field shape), and
/// <see cref="global::app.channel.serializer.filter.Transport.ForOutbound"/>
/// (re-includes [Out] properties like Signature).
/// </para>
///
/// <para>
/// Notably the merged options do NOT chain
/// <see cref="global::app.channel.serializer.filter.Sensitive.Strip"/> in —
/// PLang's own settings/sqlite store rides on this serializer to persist
/// Identity (whose PrivateKey is marked [Sensitive]). Sensitive-stripping is
/// the responsibility of channels that publish externally (HTTP responses,
/// external JSON), and lives on the base <see cref="global::app.channel.serializer.Json"/>
/// rather than the inter-actor transport serializer.
/// </para>
///
/// <para>
/// Read does NOT auto-verify — verification is the consumer's explicit step
/// (<c>signing.verify</c> action, or a channel event handler bound to
/// <c>BeforeRead</c>/<c>AfterRead</c>). The reconstructed Data has its signature
/// populated-but-unverified.
/// </para>
/// </summary>
public sealed class @this : ISerializer
{
    public string Type => "application/plang";
    public string Extension => ".plang";

    private readonly JsonSerializerOptions _outbound;
    private readonly JsonSerializerOptions _inbound;
    private readonly JsonSerializerOptions _store;
    private readonly JsonSerializerOptions _snapshot;

    public @this() : this(null) { }

    public @this(actor.context.@this? context)
    {
        var pathConverter = context != null
            ? new global::app.channel.serializer.json.Converter(context)
            : new global::app.channel.serializer.json.Converter();

        // Pass context so a lazily-deferred value slot read back through this
        // (per-actor) serializer carries the context it needs to materialize.
        _outbound = BuildOptions(
            new global::app.data.Wire(global::app.View.Out, context: context),
            pathConverter,
            global::app.channel.serializer.filter.Transport.ForOutbound);

        _inbound = BuildOptions(
            new global::app.data.Wire(global::app.View.Out, context: context),
            pathConverter,
            global::app.channel.serializer.filter.Transport.ForInbound);

        _store = BuildOptions(
            new global::app.data.Wire(global::app.View.Store, context: context),
            pathConverter,
            global::app.channel.serializer.filter.Transport.ForOutbound);

        // Snapshot durable-execution wire: Store view (local round-trip, keeps
        // [Sensitive]) but NON-signing — a snapshot is internal in-process state,
        // not an actor-boundary crossing. Adds the polymorphic IError converter
        // for the Errors trail section. Same recipe otherwise → one source of truth.
        _snapshot = BuildOptions(
            new global::app.data.Wire(global::app.View.Store, sign: false),
            pathConverter,
            global::app.channel.serializer.filter.Transport.ForOutbound);
        _snapshot.Converters.Add(new global::app.error.ErrorWire());
    }

    private static JsonSerializerOptions BuildOptions(
        global::app.data.Wire wire,
        global::app.channel.serializer.json.Converter pathConverter,
        System.Action<JsonTypeInfo> modifier)
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                wire,
                pathConverter,
            },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { modifier }
            }
        };

    /// <summary>
    /// Raw outbound options — exposed for canonicalization (crypto.Hash) so the
    /// signed bytes match the wire bytes.
    /// </summary>
    internal JsonSerializerOptions OutboundOptions => _outbound;

    /// <summary>
    /// Store-view options — the [Store]-including, Sensitive-keeping recipe used
    /// for local persistence. Exposed so the snapshot wire serializer
    /// (<see cref="global::app.snapshot.Io"/>) layers its IError converter on top
    /// of this one recipe rather than duplicating the camelCase + Wire + path +
    /// transport-modifier construction.
    /// </summary>
    internal JsonSerializerOptions StoreOptions => _store;

    /// <summary>
    /// Options for the snapshot durable-execution wire — Store view, non-signing,
    /// with the polymorphic <see cref="global::app.error.ErrorWire"/> for the
    /// Errors trail. Consumed by <c>App.SnapshotToWire</c>/<c>SnapshotFromWire</c>.
    /// </summary>
    internal JsonSerializerOptions SnapshotOptions => _snapshot;

    /// <summary>
    /// Context-less fallback instance — used by callers (crypto.Hash,
    /// Data.CompressAsync/DecompressAsync) that may run outside an actor
    /// scope and still need the canonical wire shape. Single source of truth
    /// so both Hash and Transport route through the same OutboundOptions
    /// (no drift if the construction recipe changes).
    /// </summary>
    public static readonly @this ContextLessFallback = new @this();

    public async Task<global::app.data.@this> SerializeAsync(Stream stream, global::app.data.@this data, CancellationToken cancellationToken = default)
    {
        try
        {
            // Materialize lazy reference fundamentals (image bytes) above the
            // STJ converter wall — the sync Wire.Write below cannot await.
            var loadError = await data.Load();
            if (loadError != null) return loadError;
            await JsonSerializer.SerializeAsync(stream, data, _outbound, cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    public async Task<global::app.data.@this> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return global::app.data.@this.Ok();
            var v = await JsonSerializer.DeserializeAsync<global::app.data.@this>(stream, _inbound, cancellationToken);
            return global::app.data.@this.Ok(v);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public async Task<global::app.data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : global::app.type.item.@this
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return global::app.data.@this<T>.Ok(default!);
            var v = await JsonSerializer.DeserializeAsync<T>(stream, _inbound, cancellationToken);
            return global::app.data.@this<T>.Ok(v!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return global::app.data.@this<T>.FromError(new error.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this<global::app.type.text.@this> Serialize(global::app.data.@this data)
    {
        try
        {
            return global::app.data.@this<global::app.type.text.@this>.Ok(JsonSerializer.Serialize(data, _outbound));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<global::app.type.text.@this>.FromError(new error.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// Serialize for local persistence (sqlite settings / identity / permission
    /// store). Uses the <see cref="global::app.View.Store"/>-bound
    /// <see cref="global::app.data.Wire"/> so every
    /// <c>[Store]</c>-tagged property — including <c>[Sensitive]</c> ones
    /// like <c>Identity.PrivateKey</c> — round-trips. No observer on the
    /// local persistence path, so <c>[Masked]</c> is ignored too.
    /// </summary>
    public global::app.data.@this<global::app.type.text.@this> Store(global::app.data.@this data)
    {
        try
        {
            return global::app.data.@this<global::app.type.text.@this>.Ok(JsonSerializer.Serialize(data, _store));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<global::app.type.text.@this>.FromError(new error.ServiceError(
                $"Plang Store failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// Deserialize from local persistence (sqlite). Symmetric to
    /// <see cref="Store"/>; reads through the Store-view options so any
    /// [Store]-only property (re-)hydrates on the inbound side.
    /// </summary>
    public global::app.data.@this Load(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null")
                return global::app.data.@this.Ok(null);
            return global::app.data.@this.Ok(
                (object?)JsonSerializer.Deserialize<global::app.data.@this>(s, _store)!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Plang Load failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// Typed load — symmetric to <see cref="Load(string)"/> when the caller
    /// knows the wrapped type (e.g. <c>Load&lt;Identity&gt;</c> from the
    /// identity table). [Store]-only properties hydrate on the inbound side.
    /// </summary>
    // Store load: the persisted value IS a Data, so the result is that Data (T : data),
    // not Data<T> (which would force a Data<Data> the constraint forbids). Tuple result.
    public (T? Value, error.IError? Error) Load<T>(string s) where T : global::app.data.@this
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null") return (default, null);
            return (JsonSerializer.Deserialize<T>(s, _store)!, null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return (default, new error.ServiceError(
                $"Plang Load failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this Deserialize(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null") return global::app.data.@this.Ok();
            return global::app.data.@this.Ok(JsonSerializer.Deserialize<global::app.data.@this>(s, _inbound));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this.FromError(new error.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    public global::app.data.@this<T> Deserialize<T>(string s) where T : global::app.type.item.@this
    {
        try
        {
            if (string.IsNullOrEmpty(s) || s == "null") return global::app.data.@this<T>.Ok(default!);
            return global::app.data.@this<T>.Ok(JsonSerializer.Deserialize<T>(s, _inbound)!);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return global::app.data.@this<T>.FromError(new error.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }
}
