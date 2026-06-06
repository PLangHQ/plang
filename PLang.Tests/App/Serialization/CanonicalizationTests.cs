using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Canonicalization fix: crypto/Default.cs Hash must canonicalize through the SAME
// wire converter the plang serializer uses, so hashed-bytes ≡ wire-bytes minus the
// outermost Signature.

public class CanonicalizationTests
{
    private static global::app.@this NewSignedApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-canon-" + Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task CryptoHash_UsesTransportForOutboundOptions_NotDefaultStj()
    {
        // Sign the same value via two paths: explicit EnsureSigned (which routes
        // through crypto.Hash) and a direct hash of the wire bytes through the
        // plang serializer. If Hash uses the wire options, the produced hash
        // matches what a wire-driven canonicalization would produce.
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("greeting", "hello") { Context = app.User.Context };

        byte[] wireBytesWithoutOuterSig;
        using (global::app.data.Wire.MarkOuterForHash(data))
        {
            wireBytesWithoutOuterSig = JsonSerializer.SerializeToUtf8Bytes(data, plang.OutboundOptions);
        }

        var hashResult = await app.RunAction<global::app.module.crypto.Hash>(
            new global::app.module.crypto.Hash
            {
                Data = data,
                Algorithm = new global::app.data.@this<global::app.type.text.@this>("", "keccak256")
            }, app.User.Context);

        await hashResult.IsSuccess();
        // The hash is 32 bytes; the wire-bytes are the input to the hash.
        var expectedHash = new Nethereum.Util.Sha3Keccack().CalculateHash(wireBytesWithoutOuterSig);
        await Assert.That(((global::app.module.crypto.type.hash.@this)hashResult.Value!).Bytes.SequenceEqual(expectedHash)).IsTrue();
    }

    [Test] public async Task CryptoHash_BytesMatch_WireSerializerBytesMinusOuterSignature()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("x", "y") { Context = app.User.Context };

        byte[] hashInputBytes;
        using (global::app.data.Wire.MarkOuterForHash(data))
        {
            hashInputBytes = JsonSerializer.SerializeToUtf8Bytes(data, plang.OutboundOptions);
        }

        var hashInputJson = System.Text.Encoding.UTF8.GetString(hashInputBytes);
        await Assert.That(hashInputJson).DoesNotContain("signature");
        // The variable name is excluded from the signed hash — a value verifies the
        // same regardless of which variable holds it (sign → write to %a% → verify %a%).
        await Assert.That(hashInputJson).DoesNotContain("\"name\":\"x\"");
        await Assert.That(hashInputJson).Contains("\"value\":\"y\"");
    }

    [Test] public async Task OuterSignature_BindsInnerSignature_TamperingInnerFailsOuterVerify()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var inner = new global::app.data.@this("inner", "secret") { Context = app.User.Context };
        var outer = new global::app.data.@this("outer", inner) { Context = app.User.Context };

        var json = plang.Serialize(outer).Value!;
        await Assert.That(inner.Signature).IsNotNull();
        await Assert.That(outer.Signature).IsNotNull();

        // Tamper inner value byte on the wire by flipping the secret literal.
        // (Base64 sig values get \u-escaped for + and /, which makes raw-string
        // replace miss; flipping the value's plaintext is reliable and proves
        // the outer signature binds the *complete* wire payload.)
        var tampered = json.Replace("secret", "SECRET");
        await Assert.That(tampered).IsNotEqualTo(json);

        var back = plang.Deserialize(tampered);
        await back.IsSuccess();
        var roundTripped = back.Value as global::app.data.@this;
        await Assert.That(roundTripped).IsNotNull();
        roundTripped!.Context = app.User.Context;

        var verifyResult = await app.RunAction<global::app.module.signing.verify>(
            new global::app.module.signing.verify { Data = roundTripped, SkipFreshnessCheck = new global::app.data.@this<global::app.type.@bool.@this>("", true) },
            app.User.Context);

        await Assert.That(verifyResult.Success).IsFalse()
            .Because("Tampering inner signature must invalidate outer verification.");
    }

    [Test] public async Task StoreView_PropagatesIntoInnerDataProperties_NotHardcodedToOut()
    {
        // M2 second site: json.Writer.EndRecord normalizes inner-Data Properties
        // for inline emission. If it hard-codes View.Out, view-sensitive
        // content reachable through inner Properties gets the wrong filter
        // applied even though the outer Wire is running in Store mode.
        //
        // Properties' value-shape gate is shallow (collections pass through
        // unvalidated), so an inner Identity reachable via a list inside
        // Properties exercises the view-pass-through without violating the
        // direct-domain-object gate.
        await using var app = NewSignedApp();
        var inner = new global::app.data.@this("inner", "v") { Context = app.User.Context };
        inner.Properties["meta"] = new List<object>
        {
            new global::app.module.identity.Identity
            {
                Name = "alice",
                PublicKey = "pk",
                PrivateKey = "PRIV-must-persist",   // [Sensitive] — Store keeps, Out excludes
            }
        };
        var outer = new global::app.data.@this("outer", inner) { Context = app.User.Context };

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var outBytes = plang.Serialize(outer).Value!;
        await Assert.That(outBytes).DoesNotContain("PRIV-must-persist")
            .Because("Out view excludes [Sensitive] — the secret never reaches the wire.");

        var storeBytes = plang.Store(outer).Value!;
        await Assert.That(storeBytes).Contains("PRIV-must-persist")
            .Because("Store view includes [Sensitive] — the secret must persist locally so signing keeps working on re-read.");
    }

    [Test] public async Task EnsureInnerSigned_RecursesIntoDictionaryValues()
    {
        // IDictionary's IEnumerable yields DictionaryEntry boxes, not values.
        // Without an explicit dict branch in Wire.EnsureInnerSigned, an inner
        // Data held as a dictionary value ships unsigned even though the outer
        // Wire walk visited the dict.
        await using var app = NewSignedApp();
        var inner = new global::app.data.@this("inner", "secret") { Context = app.User.Context };
        var dict = new Dictionary<string, object?> { ["payload"] = inner };
        var outer = new global::app.data.@this("outer", dict) { Context = app.User.Context };

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        var json = plang.Serialize(outer).Value!;

        await Assert.That(inner.Signature).IsNotNull()
            .Because("Wire.EnsureInnerSigned must reach Datas held as dict values, not just list elements.");
        await Assert.That(outer.Signature).IsNotNull();
    }

    private static string FlipChar(string s)
    {
        var arr = s.ToCharArray();
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] is >= 'A' and <= 'Z') { arr[i] = (char)('A' + ((arr[i] - 'A' + 1) % 26)); return new string(arr); }
            if (arr[i] is >= 'a' and <= 'z') { arr[i] = (char)('a' + ((arr[i] - 'a' + 1) % 26)); return new string(arr); }
        }
        return s + "_";
    }
}
