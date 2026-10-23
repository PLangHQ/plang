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
/// <para>The format also reads a value slot a <c>.pr</c> captured still encoded (<see cref="Read"/>).</para>
/// </summary>
public sealed class @this : global::app.type.kind.@this<global::app.type.item.wire.@this>
{
    public @this(global::app.type.@this owner) : base("plang", owner) { }

    public override System.Collections.Generic.IReadOnlyList<string> Mime => ["application/plang", "application/plang+json"];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => [".plang"];
    public override bool Compressible => true;

    /// <summary>Its content is characters — json, one whole Data per line on a framed channel.</summary>
    public override bool IsText => true;

    public override async System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View view,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
    {
        try
        {
            // Sign-if-missing at the I/O boundary: a Data crossing in a real actor scope rides in ONE signature
            // layer; skipped with no actor (an internal write) or when it holds one already — a Data changed after
            // signing goes out with the layer it holds and is refused where it arrives, never re-signed here.
            if (context.Actor != null && data.Signature == null)
            {
                var signed = await new global::app.goal.step.action.@this(
                    new global::app.module.signing.sign(context) { Data = data,
                        // Hash in the view being written, so the verifier (re-hashing the wire-reconstructed bag in
                        // the same view) gets matching bytes.
                        StoreView = new global::app.data.@this<global::app.type.item.@bool.@this>("", view == global::app.View.Store, context: context) },
                    context).Start(context);
                // A Data that should cross signed and couldn't be is the write's failure — never sent unsigned. A failed
                // result signs as itself, so what tells is the signature it holds.
                if (signed.Signature == null) return signed;
                data = signed;
            }
            await using var utf8 = new Utf8JsonWriter(stream);
            var writer = new Writer(utf8, view, context);
            if (data.Signature is { } layer)
                await layer.Over(data).Output(writer);
            else
                await data.Output(writer, layer: true);
            await utf8.FlushAsync(ct);
            return context.Ok();
        }
        // a write that refused itself (a getter that threw, a type with no wire contract) is the encode's answer
        catch (global::app.error.AppException ex) { return context.Error(ex.Error); }
        // what the json writer itself refuses (System.Text.Json's own throw, which carries no Error)
        catch (JsonException ex)
        {
            return context.Error(new global::app.error.ServiceError(
                $"Plang serialize failed: {ex.Message}", "PlangSerializeError", 400) { Exception = ex });
        }
    }

    /// <summary>The whole Data the bytes are — reconstructed, its signature verified; between actors (Out) a Data
    /// without a signature is refused. A Data carries its own facts, so a <paramref name="template"/> and an
    /// <paramref name="origin"/> ask nothing of it.</summary>
    public override async System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, global::app.View view, string name = "",
        System.Threading.CancellationToken ct = default, global::app.type.item.template.kind.@this? template = null, global::app.type.item.path.@this? origin = null)
    {
        try
        {
            if (raw.Length == 0) return context.Ok();
            // The container IS a Data — the reconstruction itself, never an Ok around it.
            // a signature read live is good for the signing setting's window: read here, where the read can wait for it
            var window = view == global::app.View.Store ? null
                : (await context.Setting.Get<global::app.module.signing.setting.@this>()).Expiry;
            var read = new global::app.data.Wire(view, context: context, deferVerify: true, window: window).ReadBuffered(raw);
            if (read == null) return context.Ok();

            // Deferred verify: the sync reader answered the Data holding the signature it arrived in (it can't
            // await inside a ref-struct reader); verify it now, awaited. A failed verify answers a fresh error,
            // so a Data this answers holds only a signer it verified.
            if (read.Signature != null)
            {
                var verified = await new global::app.goal.step.action.@this(
                    new global::app.module.signing.verify(context) { Data = read }, context).Start(context);
                if (!verified.Success)
                    return context.Error(verified.Error ?? new global::app.error.ServiceError(
                        "Signature verification failed", "SignatureInvalid", 400));
            }
            // Between actors (the Out view) a Data rides signed — this format signs every one it writes — so
            // content that arrives without a signature is refused. plang's own store (Store) keeps what it wrote.
            else if (view == global::app.View.Out)
                return context.Error(new global::app.error.ServiceError(
                    "plang content arrived unsigned — between actors a Data rides signed", "UnsignedPlang", 403));
            // between actors a Data's name is no part of what crosses (not written, not signed): it is named by its
            // receiver, never by what the wire says
            if (view == global::app.View.Out) read.Name = name;
            return read;
        }
        // what the bytes say a value is, refused by that value (a signature whose hash is of no kind), or a layer no
        // reader reads — the read's answer, its Error whole
        catch (global::app.error.AppException ex) { return context.Error(ex.Error); }
        // bytes the json reader itself refuses (System.Text.Json's own throw, which carries no Error)
        catch (JsonException ex)
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
}
