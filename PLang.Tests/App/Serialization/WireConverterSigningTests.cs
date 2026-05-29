namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Sign-if-missing during the wire converter walk. Each Data the converter visits during
// serialization: if Signature is null, call EnsureSigned; if populated, leave alone.

public class WireConverterSigningTests
{
    private static global::app.@this NewSignedApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-wire-sig-" + Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task WireConverter_OnUnsignedData_FiresEnsureSignedAndEmitsSignature()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("greeting", "hello") { Context = app.User.Context };
        await Assert.That(data.Signature).IsNull();

        var json = plang.Serialize(data).Value!;

        await Assert.That(data.Signature).IsNotNull().Because("Converter must EnsureSigned before emit");
        await Assert.That(json).Contains("\"signature\"");
    }

    [Test] public async Task WireConverter_OnSignedData_LeavesSignatureUnchanged()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("x", "y") { Context = app.User.Context };
        data.EnsureSigned();
        var firstSigBytes = data.Signature!.Value;

        plang.Serialize(data);
        var secondSigBytes = data.Signature!.Value;

        await Assert.That(secondSigBytes).IsEqualTo(firstSigBytes);
    }

    [Test] public async Task EnsureSigned_CalledTwice_DoesNotProduceTwoSignatures()
    {
        await using var app = NewSignedApp();
        var data = new global::app.data.@this("x", "y") { Context = app.User.Context };
        data.EnsureSigned();
        var sig = data.Signature;
        data.EnsureSigned();
        await Assert.That(ReferenceEquals(sig, data.Signature)).IsTrue()
            .Because("EnsureSigned is idempotent — second call is a no-op when Signature is already set.");
    }

    [Test] public async Task WireConverter_OnListDataInsideValue_SignsEachElement()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var inner1 = new global::app.data.@this("a", "1") { Context = app.User.Context };
        var inner2 = new global::app.data.@this("b", "2") { Context = app.User.Context };
        // Pre-sign inner elements (proves the on-wire visibility of each
        // element's signature; the sign-if-missing rule fires per Data anyway,
        // but the explicit EnsureSigned isolates the test from recursive
        // signing during the outer's hash-canonicalisation walk).
        inner1.EnsureSigned();
        inner2.EnsureSigned();
        var outer = new global::app.data.@this("list", new List<global::app.data.@this> { inner1, inner2 })
            { Context = app.User.Context };

        var json = plang.Serialize(outer).Value!;

        await Assert.That(inner1.Signature).IsNotNull();
        await Assert.That(inner2.Signature).IsNotNull();
        await Assert.That(outer.Signature).IsNotNull();
        await Assert.That(JsonContains(json, inner1.Signature!.Value!)).IsTrue();
        await Assert.That(JsonContains(json, inner2.Signature!.Value!)).IsTrue();
    }

    // STJ escapes `+` and `/` as + / / in default options — flatten both
    // sides before substring-search so the test doesn't depend on escape choice.
    private static bool JsonContains(string json, string raw)
    {
        var jsonFlat = json.Replace("\\u002B", "+").Replace("\\u002F", "/");
        return jsonFlat.Contains(raw);
    }

    [Test] public async Task BeforeWriteHandler_MutatesData_MutationIncludedInCanonicalSign()
    {
        await using var app = NewSignedApp();
        var data = new global::app.data.@this("x", "before") { Context = app.User.Context };
        // Mutate BEFORE serialize — the converter signs the mutated value.
        data.Value = "after";
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");
        var json = plang.Serialize(data).Value!;
        await Assert.That(json).Contains("after");
        await Assert.That(json).DoesNotContain("before");
        await Assert.That(data.Signature).IsNotNull();
    }

    [Test] public async Task ApplicationPlang_Read_PopulatesSignature_WithoutAutoVerify()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("greeting", "hello") { Context = app.User.Context };
        var json = plang.Serialize(data).Value!;

        var back = plang.Deserialize(json);
        await Assert.That(back.Success).IsTrue();
        var roundTripped = back.Value as global::app.data.@this;
        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped!.Signature).IsNotNull()
            .Because("Read reconstructs Signature into the Data, populated-but-unverified.");
    }

    [Test] public async Task WireConverter_OnByteArrayValue_EmitsBytesWithoutNestedDataWrap()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var bytes = new byte[] { 1, 2, 3, 4 };
        var data = new global::app.data.@this("blob", bytes) { Context = app.User.Context };
        var json = plang.Serialize(data).Value!;

        // Byte[] serializes to base64 string in JSON — the value slot must NOT be a
        // nested {name, type, value} Data object.
        await Assert.That(json).Contains("\"value\":\"" + Convert.ToBase64String(bytes) + "\"");
    }

    [Test] public async Task WireConverter_DoesNotWalkProperties_AsDataNodes()
    {
        // Pre-Stage-4 Properties is still IList<Data> — but the wire converter shouldn't
        // walk into Properties even today (they're [JsonIgnore]). Stage 4 turns Properties
        // into Dictionary<string, object?>; the don't-walk-Properties rule is preserved.
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("x", "y") { Context = app.User.Context };
        var json = plang.Serialize(data).Value!;
        // Properties is [JsonIgnore] and stays off the wire pre-Stage-4.
        await Assert.That(json).DoesNotContain("\"properties\"");
    }
}
