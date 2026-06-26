namespace PLang.Tests.App.Serialization;

// Boundary signing (signature-as-layer). Signing is no longer a per-Data
// "sign-if-missing" walk with a Data.Signature property — a Data crossing the
// application/plang boundary within an actor scope is wrapped in a `signature`
// LAYER on write (hoisted top-level), and the layer is auto-verified + peeled on
// read. These pin that boundary behavior.

public class WireConverterSigningTests
{
    private static global::app.@this NewSignedApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-wire-sig-" + Guid.NewGuid().ToString("N")[..8]));

    private static global::app.channel.serializer.plang.@this Plang(global::app.@this app)
        => (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

    [Test] public async Task Serialize_WithinActorScope_WrapsInSignatureLayer()
    {
        await using var app = NewSignedApp();
        var data = new global::app.data.@this("greeting", "hello", context: app.User.Context);

        var json = (await Plang(app).Serialize(data).Value())!.Clr<string>()!;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        // Top-level object IS the signature layer (hoisted past the data envelope).
        await Assert.That(doc.RootElement.GetProperty("@schema").GetString()).IsEqualTo("signature");
        await Assert.That(doc.RootElement.TryGetProperty("signature", out _)).IsTrue();
        // The inner value rides under `value` as a @schema:data record.
        var inner = doc.RootElement.GetProperty("value");
        await Assert.That(inner.GetProperty("@schema").GetString()).IsEqualTo("data");
        await Assert.That(inner.GetProperty("value").GetString()).IsEqualTo("hello");
    }

    [Test] public async Task Serialize_ThenDeserialize_AutoVerifies_AndPeelsToInnerData()
    {
        await using var app = NewSignedApp();
        var data = new global::app.data.@this("greeting", "hello", context: app.User.Context);
        var json = (await Plang(app).Serialize(data).Value())!.Clr<string>()!;

        var roundTripped = Plang(app).Deserialize(json);
        await roundTripped.IsSuccess();
        // Read peeled the verified layer down to the inner data.
        await Assert.That((await roundTripped.Value())?.ToString()).IsEqualTo("hello");
    }

    [Test] public async Task Deserialize_TamperedInnerValue_FailsVerification()
    {
        await using var app = NewSignedApp();
        var data = new global::app.data.@this("greeting", "hello", context: app.User.Context);
        var json = (await Plang(app).Serialize(data).Value())!.Clr<string>()!;

        // Flip the signed inner value — the signature no longer covers the payload.
        var tampered = json.Replace("hello", "HELLO");
        await Assert.That(tampered).IsNotEqualTo(json);

        var result = Plang(app).Deserialize(tampered);
        await Assert.That(result.Success).IsFalse()
            .Because("Auto-verify on read must reject a payload whose inner value was tampered.");
    }

    [Test] public async Task Serialize_ByteArrayValue_InnerEmitsBase64_NotNestedData()
    {
        await using var app = NewSignedApp();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var data = new global::app.data.@this("blob", bytes, context: app.User.Context);
        var json = (await Plang(app).Serialize(data).Value())!.Clr<string>()!;

        // The inner data's value is the base64 string, not a nested {name,type,value}.
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var inner = doc.RootElement.GetProperty("value");
        await Assert.That(inner.GetProperty("value").GetString()).IsEqualTo(Convert.ToBase64String(bytes));
    }

    // A transport (Out-view) read of a signed payload with no actor context cannot
    // verify, so it must fail closed rather than silently peel to the inner data.
    // ContextLessFallback is the context-less transport serializer.
    [Test] public async Task Deserialize_SignatureLayer_NoActorContext_FailsClosed()
    {
        await using var app = NewSignedApp();
        var data = new global::app.data.@this("greeting", "hello", context: app.User.Context);
        var json = (await Plang(app).Serialize(data).Value())!.Clr<string>()!;

        var result = global::app.channel.serializer.plang.@this.ContextLessFallback.Deserialize(json);

        await Assert.That(result.Success).IsFalse()
            .Because("A transport read of a signed payload with no actor context must fail closed, not strip verification.");
        await Assert.That(result.Error!.Key).IsEqualTo("SignatureVerifyContextMissing");
    }
}
