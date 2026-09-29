using System.Text.Json;

namespace app.type.item.wire.kind.plang;

/// <summary>
/// plang's own format — <c>application/plang</c>: content that is a whole Data (its value, properties and
/// signature), not a value. The transport between plang actors and plang's own store.
///
/// <para>Encode signs a Data that crosses without a signature (with the asker's actor), then writes it in the
/// schema writer — a signature layer writes its own <c>@schema:&lt;kind&gt;</c> envelope, a plain Data the
/// <c>@schema:data</c> layer. Decode reads the whole Data back and verifies any signature it meets: a bad,
/// expired or wrong-key signature fails the read. The Out view enforces freshness and replay; the Store
/// view (plang's own files and store) skips the freshness window — at-rest artefacts present the same nonce
/// by design. Sensitive values are not stripped here: plang's store rides this format to keep an identity.</para>
///
/// <para>The format also reads a value slot a <c>.pr</c> captured still encoded (<see cref="Read"/>) and says
/// whether a writer writes this format (<see cref="Owns"/>), so a captured slice relays byte-identical.</para>
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this() : base("plang") { }

    protected internal override string Owner => "wire";

    public override System.Collections.Generic.IReadOnlyList<string> Mime => ["application/plang", "application/plang+json"];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => [".plang"];
    public override bool Compressible => true;

    public override async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? asked = null,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
    {
        var view = asked ?? global::app.View.Out;   // its own face: the transport's
        try
        {
            // Sign-if-missing at the I/O boundary: a Data crossing in a real actor scope rides in ONE signature
            // layer; skipped with no actor (an internal write) or when it already is one.
            if (context.Actor != null && data.Peek() is not global::app.type.item.signature.@this)
            {
                var signed = await context.App.Run(
                    new global::app.module.signing.sign(context) { Data = data,
                        // Hash in the view being written, so the verifier (re-hashing the wire-reconstructed bag in
                        // the same view) gets matching bytes.
                        StoreView = new global::app.data.@this<global::app.type.item.@bool.@this>("", view == global::app.View.Store, context: context) },
                    context);
                // A Data that should cross signed and couldn't be is the write's failure — never sent unsigned.
                if (!signed.Success) return signed;
                data = signed;
            }
            await using var utf8 = new Utf8JsonWriter(stream);
            var writer = new global::app.type.item.kind.json.Writer(utf8, view, emitsSchema: true);
            if (data.Peek() is global::app.type.item.signature.@this sig)
                await sig.Output(writer, view, context);
            else
                await data.Output(writer, view, context, layer: true);
            await utf8.FlushAsync(ct);
            return context.Ok();
        }
        catch (System.Exception ex) when (ex is JsonException or System.NotSupportedException)
        {
            return context.Error(new global::app.error.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    /// <summary>The whole Data the bytes are — reconstructed, its signature verified; between actors (Out) a Data
    /// without a signature is refused. A Data carries its own facts, so a <paramref name="template"/> asks nothing
    /// of it.</summary>
    public override async System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name = "", global::app.View view = global::app.View.Out,
        System.Threading.CancellationToken ct = default, string? template = null)
    {
        try
        {
            if (raw.Length == 0) return context.Ok();
            // The container IS a Data — the reconstruction itself, never an Ok around it.
            var read = new global::app.data.Wire(view, context: context, deferVerify: true).ReadBuffered(raw);
            if (read == null) return context.Ok();

            // Deferred verify: the sync reader stamped the unverified signature layer (it can't await inside a
            // ref-struct reader); verify it now, awaited.
            if (read.PendingVerification is { } layer)
            {
                read.PendingVerification = null;
                var verified = await context.App.Run(new global::app.module.signing.verify(context)
                {
                    Data = context.Ok(layer),
                    SkipFreshnessCheck = new global::app.data.@this<global::app.type.item.@bool.@this>("", view == global::app.View.Store),
                }, context);
                if (!verified.Success)
                    return context.Error(verified.Error ?? new global::app.error.ServiceError(
                        "Signature verification failed", "SignatureInvalid", 400));
            }
            // Between actors (the Out view) a Data rides signed — this format signs every one it writes — so
            // content that arrives without a signature is refused. plang's own store (Store) keeps what it wrote.
            else if (view == global::app.View.Out)
                return context.Error(new global::app.error.ServiceError(
                    "plang content arrived unsigned — between actors a Data rides signed", "UnsignedPlang", 403));
            return read;
        }
        catch (System.Exception ex) when (ex is JsonException or System.NotSupportedException)
        {
            return context.Error(new global::app.error.ServiceError(
                $"Plang deserialize failed: {ex.Message}", "PlangDeserializeError", 400) { Exception = ex });
        }
    }

    /// <summary>
    /// A value slot a <c>.pr</c> captured still encoded, read into its plang type: the bytes are this format's
    /// json, so the type pulls itself off a json reader over them — the door a lazy wire materializes through.
    /// </summary>
    public global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx)
    {
        var type = source.Type;
        var kind = type.kind.IsEmpty ? null : type.kind.Name;
        var typeReader = ctx.Context.App.type.list.Reader.Reader(type.Name, kind, ctx.Context);
        byte[] bytes = source.Raw as byte[] ?? System.Text.Encoding.UTF8.GetBytes(source.Raw.ToString() ?? "");
        var utf8 = new Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8);
        return typeReader.Read(ref reader, kind, ctx);
    }

    /// <summary>Whether <paramref name="writer"/> writes this format — a captured slice rides verbatim into a json
    /// writer (schema on → "plang", off → "json"); any other writer is a different format, where the slice is
    /// decoded and the value writes itself.</summary>
    public bool Owns(global::app.type.format.IWriter writer) => writer.Format is "plang" or "json";
}
