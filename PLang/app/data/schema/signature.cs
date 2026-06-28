namespace app.data.schema;

using Data = global::app.data.@this;

/// <summary>
/// The <c>@schema:signature</c> reader — an attestation layer wrapping inner data. Rebuilds
/// the layer (<see cref="global::app.type.signature.@this.FromWire"/>), AUTO-VERIFIES on read
/// (runs the verify action — a bad/expired/wrong-key signature fails the read), and peels to
/// the verified inner data. The verify is View-gated: a Store read skips the wire-freshness +
/// nonce-replay window (at-rest artifacts re-present the same nonce by design; their own
/// Expires is the time bound), a transport (Out) read keeps it.
///
/// <para>Still DOM-based (FromWire) + sync-over-async verify — the security boundary the Wire
/// owns, now dispatched like any other <c>@schema</c>. <c>options</c> feeds FromWire's inner
/// <c>Deserialize</c>.</para>
/// </summary>
public sealed class signature : ISchemaReader
{
    public string Schema => global::app.type.signature.@this.WireSchemaSignature;

    public Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx, System.Text.Json.JsonSerializerOptions options)
    {
        var context = ctx.Context;
        using var doc = System.Text.Json.JsonDocument.ParseValue(ref reader.Inner);
        var layer = global::app.type.signature.@this.FromWire(doc.RootElement, options);

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

        var inner = layer.Value;
        inner.Context = context;
        return inner;
    }
}
