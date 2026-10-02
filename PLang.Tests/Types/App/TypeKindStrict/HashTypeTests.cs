using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;
using hash = global::app.module.crypto.type.hash.@this;

namespace PLang.Tests.App.TypeKindStrict;

// `hash` is a crypto-owned type whose kind is the algorithm; crypto.hash
// returns a hash value so the digest knows how to be verified.
public class HashTypeTests
{
    [Test] public async Task HashType_Resolves_ViaRegistry()
    {
        await using var app = new global::app.@this("/test").Testing();
        var t = app.type.list["hash"];
        await Assert.That(t.Name).IsEqualTo("hash");
        await Assert.That(t.ClrType).IsEqualTo(typeof(hash));
    }

    [Test] public async Task HashType_OwnsBase64RoundTrip()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var h = new hash(bytes, "sha256");
        var roundTripped = hash.FromBase64(h.ToBase64(), "sha256");
        await Assert.That(roundTripped.DigestEquals(h)).IsTrue();
        await Assert.That(roundTripped.Algorithm).IsEqualTo("sha256");
    }

    // A digest compares to its own text (Ingi, 410): the other side is read into a digest — hex, else base64 — and
    // the bytes compared, from either side; a different digest is not equal; a text that is neither is Incomparable.
    [Test] public async Task Digest_EqualsItsHexAndBase64Text_AndNothingElse()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var bytes = System.Security.Cryptography.SHA256.HashData("abc"u8.ToArray());
        var other = System.Security.Cryptography.SHA256.HashData("abd"u8.ToArray());
        var digest = new global::app.data.@this("d", new hash(bytes, "sha256"), context: ctx);
        global::app.data.@this Text(string s) => new("t", s, context: ctx);

        await Assert.That(await digest.Compare(Text(System.Convert.ToHexString(bytes)))).IsEqualTo(global::app.data.Comparison.Equal);
        await Assert.That(await digest.Compare(Text(System.Convert.ToHexString(bytes).ToLowerInvariant()))).IsEqualTo(global::app.data.Comparison.Equal);
        await Assert.That(await digest.Compare(Text(System.Convert.ToBase64String(bytes)))).IsEqualTo(global::app.data.Comparison.Equal);
        await Assert.That(await Text(System.Convert.ToHexString(bytes)).Compare(digest)).IsEqualTo(global::app.data.Comparison.Equal);
        await Assert.That(await digest.Compare(new global::app.data.@this("o", new hash(other, "sha256"), context: ctx))).IsEqualTo(global::app.data.Comparison.NotEqual);
        await Assert.That(await digest.Compare(Text(System.Convert.ToHexString(other)))).IsEqualTo(global::app.data.Comparison.NotEqual);
        await Assert.That(await digest.Compare(Text("hello"))).IsEqualTo(global::app.data.Comparison.Incomparable);
    }

    [Test] public async Task CryptoHash_ReturnsHashValueWithAlgorithmKind()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(ctx, "crypto", "hash",
            ("data", "hello"), ("algorithm", "sha256"));
        var result = await action.Start(ctx);
        await result.IsSuccess();
        await Assert.That(result.Type!.Name).IsEqualTo("hash");
        await Assert.That(result.Type!.kind.Name).IsEqualTo("sha256");
        // The value is a hash, not bare bytes — so the live serializer renders
        // it and the builder annotates the write-to variable as `(hash)`.
        await Assert.That((await result.Value()) is hash).IsTrue();
        await Assert.That(((hash)(await result.Value())!).Algorithm).IsEqualTo("sha256");
    }

    [Test] public async Task CryptoVerify_DefaultsAlgorithmFromHashValue()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var crypto = new global::app.module.crypto.code.Default();

        // Produce a sha256 digest of a Data, then verify the SAME Data against
        // the produced hash value directly — no manual base64, no manual type
        // stamp. The algorithm rides on the hash value (sha256), so verify must
        // succeed with NO explicit Algorithm (which defaults to keccak256).
        var digest = await crypto.Hash(new global::app.module.crypto.Hash(ctx) { Data = ctx.Ok("hello"),
            Algorithm = new global::app.data.@this("Algorithm", "sha256", context: ctx).As<global::app.type.item.choice.@this<global::app.module.crypto.type.hash.kind.@this>>(),
        });
        await digest.IsSuccess();
        await Assert.That((await digest.Value()) is hash).IsTrue();
        await Assert.That(((hash)(await digest.Value())!).Algorithm).IsEqualTo("sha256");

        var verify = new global::app.module.crypto.Verify(ctx) { Data = ctx.Ok("hello"),
            Hash = digest,
        };
        await verify.Attach(null, ctx);
        var result = await verify.Start();
        await result.IsSuccess();
        await Assert.That((await result.Value())!.Value).IsTrue();
    }
}
