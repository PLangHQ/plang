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
        // hash advertises its algorithms on the registry entity — how C#
        // resolves the kind when validating `verify %bla%` and how getTypes
        // maps a produced variable. The per-step LLM prompt table does NOT
        // carry them (hash is a result type, not a fundamental the LLM emits).
        await using var app = new PLangEngine("/test");
        var kinds = app.Type["hash"].Kinds!;
        await Assert.That(kinds).Contains("sha256");
        await Assert.That(kinds).Contains("keccak256");
    }

    [Test] public async Task HashKinds_DoNotLeakIntoTheLlmPromptVocabulary()
    {
        // The prompt's kind table is scoped to fundamentals; a result type's
        // algorithms are noise the LLM never chooses from.
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Module.Schema.Build().Kinds.ContainsKey("hash")).IsFalse();
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
            Algorithm = new global::app.data.@this<global::app.type.text.@this>("Algorithm", "sha256"),
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
