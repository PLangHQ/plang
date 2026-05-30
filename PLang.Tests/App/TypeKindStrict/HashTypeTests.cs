using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict;

// Stage 7: `hash` is a first-class type whose kind is the algorithm.
public class HashTypeTests
{
    [Test] public async Task HashType_Resolves_ViaRegistry()
    {
        await using var app = new PLangEngine("/test");
        var t = app.Type["hash"];
        await Assert.That(t.Name).IsEqualTo("hash");
        await Assert.That(t.ClrType).IsEqualTo(typeof(global::app.type.hash.@this));
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
        var h = new global::app.type.hash.@this(bytes, "sha256");
        var roundTripped = global::app.type.hash.@this.FromBase64(h.ToBase64(), "sha256");
        await Assert.That(roundTripped.DigestEquals(h)).IsTrue();
        await Assert.That(roundTripped.Algorithm).IsEqualTo("sha256");
    }

    [Test] public async Task CryptoHash_StampsHashNameWithAlgorithmKind()
    {
        await using var app = new PLangEngine("/test");
        var ctx = app.User.Context;
        var action = TestAction.Create("crypto", "hash",
            ("data", "hello"), ("algorithm", "sha256"));
        var result = await action.RunAsync(ctx);
        await result.IsSuccess();
        await Assert.That(result.Type!.Name).IsEqualTo("hash");
        await Assert.That(result.Type!.Kind).IsEqualTo("sha256");
    }

    [Test] public async Task CryptoVerify_DefaultsAlgorithmFromHashKind()
    {
        await using var app = new PLangEngine("/test");
        var ctx = app.User.Context;
        var crypto = new global::app.module.crypto.code.Default();

        // Produce a sha256 digest of the SAME Data object verify will recompute
        // (the hash binds the Data's full wire shape, incl. its Name — so both
        // sides must hash an identically-shaped Data).
        var payload = global::app.data.@this.Ok("hello");
        var digest = crypto.Hash(new global::app.module.crypto.Hash
        {
            Context = ctx,
            Data = payload,
            Algorithm = new global::app.data.@this<string>("Algorithm", "sha256"),
        });
        await digest.IsSuccess();
        var base64 = System.Convert.ToBase64String((byte[])digest.Value!);

        // The expected-hash Data carries {hash, sha256}; verify with NO explicit
        // algorithm must default from that kind (not the keccak256 param default).
        var hashValue = new global::app.data.@this<string>("h", base64)
        {
            Type = global::app.type.@this.Create("hash", kind: "sha256"),
            Context = ctx,
        };
        var verify = new global::app.module.crypto.Verify
        {
            Context = ctx,
            Data = global::app.data.@this.Ok("hello"),
            Hash = hashValue,
        };
        var result = await verify.Run();
        await result.IsSuccess();
        await Assert.That((bool)result.Value!).IsTrue();
    }
}
