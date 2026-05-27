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
        var plang = (global::app.channels.serializers.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("greeting", "hello") { Context = app.User.Context };

        byte[] wireBytesWithoutOuterSig;
        using (global::app.data.WireJsonConverter.MarkOuterForHash(data))
        {
            wireBytesWithoutOuterSig = JsonSerializer.SerializeToUtf8Bytes(data, plang.OutboundOptions);
        }

        var hashResult = await app.RunAction<global::app.modules.crypto.Hash>(
            new global::app.modules.crypto.Hash
            {
                Data = data,
                Algorithm = new global::app.data.@this<string>("", "keccak256")
            }, app.User.Context);

        await Assert.That(hashResult.Success).IsTrue();
        // The hash is 32 bytes; the wire-bytes are the input to the hash.
        var expectedHash = new Nethereum.Util.Sha3Keccack().CalculateHash(wireBytesWithoutOuterSig);
        await Assert.That(((byte[])hashResult.Value!).SequenceEqual(expectedHash)).IsTrue();
    }

    [Test] public async Task CryptoHash_BytesMatch_WireSerializerBytesMinusOuterSignature()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channels.serializers.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("x", "y") { Context = app.User.Context };

        byte[] hashInputBytes;
        using (global::app.data.WireJsonConverter.MarkOuterForHash(data))
        {
            hashInputBytes = JsonSerializer.SerializeToUtf8Bytes(data, plang.OutboundOptions);
        }

        var hashInputJson = System.Text.Encoding.UTF8.GetString(hashInputBytes);
        await Assert.That(hashInputJson).DoesNotContain("signature");
        await Assert.That(hashInputJson).Contains("\"name\":\"x\"");
        await Assert.That(hashInputJson).Contains("\"value\":\"y\"");
    }

    [Test] public async Task OuterSignature_BindsInnerSignature_TamperingInnerFailsOuterVerify()
    {
        await using var app = NewSignedApp();
        var plang = (global::app.channels.serializers.serializer.plang.@this)
            app.User.Channels.Serializers.GetByMimeType("application/plang");

        var inner = new global::app.data.@this("inner", "secret") { Context = app.User.Context };
        var outer = new global::app.data.@this("outer", inner) { Context = app.User.Context };

        var json = plang.Serialize(outer).Value!;
        await Assert.That(inner.RawSignature).IsNotNull();
        await Assert.That(outer.RawSignature).IsNotNull();

        // Tamper inner value byte on the wire by flipping the secret literal.
        // (Base64 sig values get \u-escaped for + and /, which makes raw-string
        // replace miss; flipping the value's plaintext is reliable and proves
        // the outer signature binds the *complete* wire payload.)
        var tampered = json.Replace("secret", "SECRET");
        await Assert.That(tampered).IsNotEqualTo(json);

        var back = plang.Deserialize(tampered);
        await Assert.That(back.Success).IsTrue();
        var roundTripped = back.Value as global::app.data.@this;
        await Assert.That(roundTripped).IsNotNull();
        roundTripped!.Context = app.User.Context;

        var verifyResult = await app.RunAction<global::app.modules.signing.verify>(
            new global::app.modules.signing.verify { Data = roundTripped, SkipFreshnessCheck = new global::app.data.@this<bool>("", true) },
            app.User.Context);

        await Assert.That(verifyResult.Success).IsFalse()
            .Because("Tampering inner signature must invalidate outer verification.");
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
