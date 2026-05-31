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
        await using var app = new PLangEngine("/test");
        var t = app.Type["hash"];
        await Assert.That(t.Name).IsEqualTo("hash");
        await Assert.That(t.ClrType).IsEqualTo(typeof(hash));
    }

    [Test] public async Task HashType_AdvertisesAlgorithmKinds()
    {
        await using var app = new PLangEngine("/test");
        var kinds = app.Module.Schema.Build().Kinds;
        await Assert.That(kinds.ContainsKey("hash")).IsTrue();
        await Assert.That(kinds["hash"]).Contains("sha256");
        await Assert.That(kinds["hash"]).Contains("keccak256");
    }

    [Test] public async Task HashType_OwnsBase64RoundTrip()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var h = new hash(bytes, "sha256");
        var roundTripped = hash.FromBase64(h.ToBase64(), "sha256");
        await Assert.That(roundTripped.DigestEquals(h)).IsTrue();
        await Assert.That(roundTripped.Algorithm).IsEqualTo("sha256");
    }

    [Test] public async Task CryptoHash_ReturnsHashValueWithAlgorithmKind()
    {
        await using var app = new PLangEngine("/test");
        var ctx = app.User.Context;
        var action = TestAction.Create("crypto", "hash",
            ("data", "hello"), ("algorithm", "sha256"));
        var result = await action.RunAsync(ctx);
        await result.IsSuccess();
        await Assert.That(result.Type!.Name).IsEqualTo("hash");
        await Assert.That(result.Type!.Kind).IsEqualTo("sha256");
        // The value is a hash, not bare bytes — so the live serializer renders
        // it and the builder annotates the write-to variable as `(hash)`.
        await Assert.That(result.Value is hash).IsTrue();
        await Assert.That(((hash)result.Value!).Algorithm).IsEqualTo("sha256");
    }

    [Test] public async Task CryptoVerify_DefaultsAlgorithmFromHashValue()
    {
        await using var app = new PLangEngine("/test");
        var ctx = app.User.Context;
        var crypto = new global::app.module.crypto.code.Default();

        // Produce a sha256 digest of a Data, then verify the SAME Data against
        // the produced hash value directly — no manual base64, no manual type
        // stamp. The algorithm rides on the hash value (sha256), so verify must
        // succeed with NO explicit Algorithm (which defaults to keccak256).
        var digest = crypto.Hash(new global::app.module.crypto.Hash
        {
            Context = ctx,
            Data = global::app.data.@this.Ok("hello"),
            Algorithm = new global::app.data.@this<string>("Algorithm", "sha256"),
        });
        await digest.IsSuccess();
        await Assert.That(digest.Value is hash).IsTrue();
        await Assert.That(((hash)digest.Value!).Algorithm).IsEqualTo("sha256");

        var verify = new global::app.module.crypto.Verify
        {
            Context = ctx,
            Data = global::app.data.@this.Ok("hello"),
            Hash = digest,
        };
        var result = await verify.Run();
        await result.IsSuccess();
        await Assert.That((bool)result.Value!).IsTrue();
    }
}
