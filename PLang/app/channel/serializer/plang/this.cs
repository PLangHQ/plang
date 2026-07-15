using System.Text.Json;

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
/// Read auto-verifies: any <c>@schema:signature</c> payload it encounters runs
/// the <c>signing.verify</c> action before the inner data is peeled out — a
/// bad/expired/wrong-key signature fails the read. Freshness + nonce-replay are
/// enforced on the Out (transport) view; the Store view skips the freshness
/// window because at-rest artifacts re-present the same nonce by design (their
/// own <c>Expires</c> is the time bound). A transport read with no actor context
/// cannot verify, so it <b>fails closed</b> — a signed payload is never unwrapped
/// without verification. At-rest (Store) reads are made context-less by the
/// settings/permission store and are trusted on read (tampering them requires
/// local-filesystem write); verifying at-rest signatures needs the actor context
/// carried into the store read. Production transport reads always carry a context
/// via the per-actor serializer.
/// </para>
/// </summary>
public sealed class @this : ITransport
{
    public string Type => "application/plang";
    public string Extension => ".plang";

    // The Wire for each read view — held so the buffer-owning entry drives it directly.
    private readonly global::app.data.Wire _inboundWire;
    private readonly global::app.data.Wire _storeWire;

    // The serializer is born with the actor context it writes toward — it never
    // reaches into the data it is handed for a context. A data crossing this
    // (per-actor) serializer belongs to this same context.
    private readonly actor.context.@this _context;

    public @this(actor.context.@this context)
    {
        _context = context;

        // The read wires — the buffer-owning entry (ReadBuffered) drives them directly.
        // deferVerify: this serializer reads through the async DeserializeAsync, so a signed
        // Data's verify runs there (awaited) instead of sync-waiting inside the ref-struct reader.
        _inboundWire = new global::app.data.Wire(global::app.View.Out, context: context, deferVerify: true);
        _storeWire = new global::app.data.Wire(global::app.View.Store, context: context, deferVerify: true);
    }

    /// <summary>
    /// View-aware stream write — the one place a Data drives its own wire via data.Output
    /// (Out = transport, Store = local persistence). Lazy refs are materialized up-front
    /// (await Load) so data.Output never meets an unresolved reference.
    /// </summary>
    public async Task<global::app.data.@this> SerializeAsync(Stream stream, global::app.data.@this data, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            // Sign-if-missing at the I/O boundary — a clean await. A Data crossing
            // application/plang in a real actor scope is wrapped in ONE signature layer;
            // skipped when no actor (internal serialize) or already a layer.
            if (_context.Actor != null && data.Peek() is not global::app.type.item.signature.@this)
            {
                var signResult = await _context.App.Run(
                    new app.module.action.signing.sign(_context) { Data = data,
                        // Hash in the view we're serializing in, so the verifier (re-hashing the
                        // wire-reconstructed bag in the same view) gets matching bytes.
                        StoreView = new app.data.@this<global::app.type.item.@bool.@this>("", view == global::app.View.Store, context: _context) },
                    _context);
                if (signResult.Success) data = signResult;
            }
            await using var utf8 = new Utf8JsonWriter(stream);
            var writer = new global::app.channel.serializer.json.Writer(
                utf8, view, _context.App.Type.Renderer, emitsSchema: true);
            // A layer (signature) writes its OWN @schema:<kind> envelope; a plain Data
            // writes the @schema:data layer. The layer-vs-data choice lives here, at the
            // serializer boundary — data.Output stays clean (@schema:data only).
            if (data.Peek() is global::app.type.item.signature.@this sig)
                await sig.Output(writer, view, _context);
            else
                await data.Output(writer, view, _context, layer: true);
            await utf8.FlushAsync(cancellationToken);
            return _context.Ok();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return _context.Error(new error.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// Write a BARE item (a goal → its <c>.pr</c>) — the item drives its own wire via <c>Output</c>,
    /// NOT wrapped in a Data envelope and NOT signed. The write counterpart to the goal reader's bare
    /// read: <c>goal.Output(Store)</c> emits the structural <c>{name, steps, …}</c> the reader expects.
    /// </summary>
    public async Task SerializeItemAsync(Stream stream, global::app.type.item.@this item,
        global::app.View view = global::app.View.Store, CancellationToken cancellationToken = default)
    {
        await using var utf8 = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        var writer = new global::app.channel.serializer.json.Writer(
            utf8, view, _context.App.Type.Renderer, emitsSchema: true);
        await item.Output(writer, view, _context);
        await utf8.FlushAsync(cancellationToken);
    }

    public async Task<global::app.data.@this> DeserializeAsync(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default)
    {
        try
        {
            if (stream.CanSeek && stream.Length == 0) return _context.Ok();
            // The container IS a Data — return the reconstruction itself, never
            // an Ok envelope around it (the bare-Data double-wrap the store seam rejects).
            // Own the buffer: read the bytes and drive the Wire read directly. With the buffer
            // in hand the read slices value slots raw (RawValue); a nested Data reads through
            // the same schema-dispatch entry, no STJ.
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms, cancellationToken);
                bytes = ms.ToArray();
            }
            if (bytes.Length == 0) return _context.Ok();
            var wire = view == global::app.View.Store ? _storeWire : _inboundWire;
            var v = wire.ReadBuffered(bytes);
            if (v == null) return _context.Ok();

            // Deferred verify: the sync reader stamped the unverified signature layer here
            // (it can't await inside a ref-struct reader). Verify it now, async — no sync-wait,
            // so parallel reads never starve the threadpool. A bad/expired/wrong-key signature
            // fails the read exactly as the inline path did.
            if (v.PendingVerification is { } layer)
            {
                v.PendingVerification = null;
                var carrier = _context.Ok(layer);
                carrier.Context = _context;
                var verifyAction = new global::app.module.action.signing.verify(_context)
                {
                    Data = carrier,
                    SkipFreshnessCheck = new global::app.data.@this<global::app.type.item.@bool.@this>(
                        "", view == global::app.View.Store),
                };
                var verifyResult = await _context.App.Run(verifyAction, _context);
                if (!verifyResult.Success)
                    return _context.Error(verifyResult.Error ?? new error.ServiceError(
                        "Signature verification failed", "SignatureInvalid", 400));
            }
            return v;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            return _context.Error(new error.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// Typed read — borns the stored value at its OWN wire type through the same
    /// path as a base read (Wire.ReadBody, the .pr mechanism), then hands it back
    /// as a <see cref="global::app.data.@this{T}"/> via <c>As&lt;T&gt;</c>: a typed
    /// FACE over the born value, with NO resolution and NO value copy. The lift to
    /// T (<c>await data.Value()</c>) is the consumer's job at the leaf — the store
    /// never processes the item. Deserializing the base Data (never <c>Data&lt;T&gt;</c>)
    /// sidesteps the lossy typed re-wrap and works even when T is abstract
    /// (<c>Get&lt;item&gt;</c>), since <c>As&lt;T&gt;</c> builds <c>Data&lt;T&gt;</c>,
    /// never an instance of T.
    /// </summary>
    public async Task<global::app.data.@this<T>> DeserializeAsync<T>(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var data = await DeserializeAsync(stream, view, cancellationToken);
        if (!data.Success) return global::app.data.@this<T>.From(data);
        return data.As<T>();
    }

    /// <summary>
    /// Reads a held value's bytes into its plang type — the <c>.pr</c> wire is JSON,
    /// so it makes a <see cref="global::app.channel.serializer.json.Reader"/> over the
    /// bytes and lets the type pull itself off it (a type with no reader yet borns its
    /// natural shape off the same pass). This is the door a lazy <c>source</c>
    /// materializes through.
    /// </summary>
    public global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx)
    {
        var type = source.Type;
        var typeReader = ctx.Context.App.Type.Reader.Reader(type.Name, type.Kind?.Name, ctx.Context);
        byte[] bytes = source.Raw as byte[] ?? System.Text.Encoding.UTF8.GetBytes(source.Raw.ToString() ?? "");
        var utf8 = new Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.channel.serializer.json.Reader(utf8);
        return typeReader.Read(ref reader, type.Kind?.Name, ctx);
    }

    // A wire slice this transport captured is json/plang text — it rides verbatim into a json
    // writer (schema on → "plang", off → "json"). A "text" (or any other) writer is a different
    // format, so the wire materializes there instead of dumping its quoted document slice.
    public bool Owns(global::app.channel.serializer.IWriter writer) => writer.Format is "plang" or "json";
}
