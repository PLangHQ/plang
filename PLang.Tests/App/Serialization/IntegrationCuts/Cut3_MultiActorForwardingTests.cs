namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-serialize-cleanup — Integration Cut 3: Multi-actor forwarding chain.

public class Cut3_MultiActorForwardingTests
{
    private static global::app.@this NewApp(string label) =>
        new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut3-" + label + "-" + Guid.NewGuid().ToString("N")[..8]));

    private record Chain(
        global::app.@this AppA,
        global::app.@this AppB,
        global::app.@this AppC,
        global::app.data.@this Inner,
        global::app.data.@this Outer,
        string OuterWire,
        global::app.data.@this RoundTripped);

    private static Chain BuildChain()
    {
        var a = NewApp("a");
        var b = NewApp("b");
        var c = NewApp("c");

        var plangA = (global::app.channel.serializer.plang.@this)
            a.User.Channel.Serializers.GetByMimeType("application/plang");

        // A: sign inner via its identity.
        var inner = new global::app.data.@this("user", "Ingi") { Context = a.User.Context };
        inner.EnsureSigned();
        var aIdentity = inner.Signature!.Identity;

        // B: wrap into outer carrying B's identity. The walk-into-inner sees
        // inner already-signed and skips re-signing (forwarding preserves provenance).
        var plangB = (global::app.channel.serializer.plang.@this)
            b.User.Channel.Serializers.GetByMimeType("application/plang");
        var outer = new global::app.data.@this("forwarded", inner) { Context = b.User.Context };
        var outerWire = plangB.Serialize(outer).Value!;

        // C: receive bytes, reconstruct.
        var plangC = (global::app.channel.serializer.plang.@this)
            c.User.Channel.Serializers.GetByMimeType("application/plang");
        var deserResult = plangC.Deserialize(outerWire);
        var roundTripped = (global::app.data.@this)deserResult.Value!;

        return new Chain(a, b, c, inner, outer, outerWire, roundTripped);
    }

    [Test] public async Task Cut3_OuterData_CarriesForwardersSigningIdentity()
    {
        var chain = BuildChain();
        await using (chain.AppA) await using (chain.AppB) await using (chain.AppC)
        {
            await Assert.That(chain.RoundTripped.Signature).IsNotNull();
            await Assert.That(chain.RoundTripped.Signature!.Identity).IsEqualTo(chain.Outer.Signature!.Identity);
            await Assert.That(chain.Outer.Signature!.Identity).IsNotEqualTo(chain.Inner.Signature!.Identity);
        }
    }

    [Test] public async Task Cut3_InnerData_RetainsOriginalSignersIdentityAfterWrap()
    {
        var chain = BuildChain();
        await using (chain.AppA) await using (chain.AppB) await using (chain.AppC)
        {
            var innerAfter = chain.RoundTripped.Value as global::app.data.@this;
            await Assert.That(innerAfter).IsNotNull();
            await Assert.That(innerAfter!.Signature).IsNotNull();
            await Assert.That(innerAfter.Signature!.Identity).IsEqualTo(chain.Inner.Signature!.Identity);
        }
    }

    [Test] public async Task Cut3_BothSignatures_VerifyIndependently()
    {
        var chain = BuildChain();
        await using (chain.AppA) await using (chain.AppB) await using (chain.AppC)
        {
            chain.RoundTripped.Context = chain.AppB.User.Context;
            var outerVerify = await chain.AppB.RunAction<global::app.module.signing.verify>(
                new global::app.module.signing.verify
                {
                    Data = chain.RoundTripped,
                    SkipFreshnessCheck = new global::app.data.@this<bool>("", true)
                }, chain.AppB.User.Context);
            await Assert.That(outerVerify.Success).IsTrue();

            var innerAfter = (global::app.data.@this)chain.RoundTripped.Value!;
            innerAfter.Context = chain.AppA.User.Context;
            var innerVerify = await chain.AppA.RunAction<global::app.module.signing.verify>(
                new global::app.module.signing.verify
                {
                    Data = innerAfter,
                    SkipFreshnessCheck = new global::app.data.@this<bool>("", true)
                }, chain.AppA.User.Context);
            await Assert.That(innerVerify.Success).IsTrue();
        }
    }

    [Test] public async Task Cut3_TamperingInnerSignatureSubObject_FailsOuterVerify()
    {
        var chain = BuildChain();
        await using (chain.AppA) await using (chain.AppB) await using (chain.AppC)
        {
            // Find the inner Data's literal value and flip it to mutate the canonical body.
            var tampered = chain.OuterWire.Replace("\"Ingi\"", "\"INGI\"");
            await Assert.That(tampered).IsNotEqualTo(chain.OuterWire);

            var plangB = (global::app.channel.serializer.plang.@this)
                chain.AppB.User.Channel.Serializers.GetByMimeType("application/plang");
            var back = plangB.Deserialize(tampered);
            await Assert.That(back.Success).IsTrue();
            var restored = (global::app.data.@this)back.Value!;
            restored.Context = chain.AppB.User.Context;

            var verify = await chain.AppB.RunAction<global::app.module.signing.verify>(
                new global::app.module.signing.verify
                {
                    Data = restored,
                    SkipFreshnessCheck = new global::app.data.@this<bool>("", true)
                }, chain.AppB.User.Context);
            await Assert.That(verify.Success).IsFalse();
        }
    }
}
