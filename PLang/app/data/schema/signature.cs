namespace app.data.schema;

using Data = global::app.data.@this;

/// <summary>
/// The <c>@schema:signature</c> reader — an attestation layer wrapping inner data. STREAMS the
/// layer's fields off the reader and reads the inner <c>value</c> through the data reader, then
/// AUTO-VERIFIES on read (runs the verify action — a bad/expired/wrong-key signature fails the
/// read) and peels to the verified inner data. The verify is View-gated: a Store read skips the
/// wire-freshness + nonce-replay window (at-rest artifacts re-present the same nonce by design;
/// their own Expires is the time bound), a transport (Out) read keeps it.
///
/// <para>Sync-over-async verify — the security boundary the Wire owns, now dispatched like any
/// other <c>@schema</c>. <c>options</c> only feeds the inner data read's goal.call TEMP.</para>
/// </summary>
public sealed class signature : ISchemaReader
{
    public string Schema => global::app.type.signature.@this.WireSchemaSignature;

    public Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
    {
        var context = ctx.Context;

        // Stream the layer's fields off the reader (no FromWire/DOM). The inner `value` IS a
        // Data — it reads through the data reader (recurse), so the verify re-hashes the same
        // reconstruction the writer produced.
        global::app.type.text.@this algorithm = new("ed25519"), nonce = new(""), identity = new("");
        System.DateTimeOffset created = default;
        System.DateTimeOffset? expires = null;
        string hashAlgo = "keccak256";
        byte[] hashValue = System.Array.Empty<byte>();
        global::app.type.binary.@this sig = new(System.Array.Empty<byte>());
        global::app.type.list.@this? contracts = null;
        Data inner = Data.Ok((object?)null);

        reader.BeginObject();
        while (reader.NextName(out var key))
        {
            switch (key.ToLowerInvariant())
            {
                case "@schema": reader.Skip(); break;
                case "type": algorithm = new(reader.String()); break;
                case "nonce": nonce = new(reader.String()); break;
                case "created": created = reader.DateTimeOffset(); break;
                case "expires": expires = reader.Null() ? null : reader.DateTimeOffset(); break;
                case "identity": identity = new(reader.String()); break;
                case "contracts":
                {
                    reader.BeginArray();
                    var items = new System.Collections.Generic.List<Data>();
                    while (reader.NextElement())
                        items.Add(Data.Ok(new global::app.type.text.@this(reader.String())));
                    reader.EndArray();
                    contracts = new global::app.type.list.@this(items);
                    break;
                }
                case "hash":
                {
                    reader.BeginObject();
                    while (reader.NextName(out var hk))
                    {
                        if (hk == "type") hashAlgo = reader.String();
                        else if (hk == "value") hashValue = global::app.type.signature.@this.SafeBase64(reader.String());
                        else reader.Skip();
                    }
                    reader.EndObject();
                    break;
                }
                case "signature": sig = new(global::app.type.signature.@this.SafeBase64(reader.String())); break;
                case "value": inner = new global::app.data.reader.@this().Read(ref reader, ctx); break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();

        var layer = new global::app.type.signature.@this(
            inner, algorithm, nonce, new global::app.type.datetime.@this(created), identity,
            new global::app.module.crypto.type.hash.@this(hashValue, hashAlgo), sig,
            expires is { } ex ? new global::app.type.datetime.@this(ex) : null, contracts);

        // The inner data is re-hashed during verify (canonicalized through the wire), so it
        // needs the actor context the same way the outer does.
        layer.Value.Context = context;

        var carrier = Data.Ok(layer);
        carrier.Context = context;
        var verifyAction = new global::app.module.signing.verify
        {
            Data = carrier,
            SkipFreshnessCheck = new global::app.data.@this<global::app.type.@bool.@this>(
                "", ctx.View == global::app.View.Store),
        };
        var verifyResult = context.App
            .RunAction(verifyAction, context)
            .GetAwaiter().GetResult();
        if (!verifyResult.Success)
            return Data.FromError(verifyResult.Error
                ?? new global::app.error.ServiceError("Signature verification failed", "SignatureInvalid", 400));

        var peeled = layer.Value;
        peeled.Context = context;
        return peeled;
    }
}
