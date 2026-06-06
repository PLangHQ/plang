using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.module.signing;
using data = global::app.data.@this;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 3 — signing verifies independent of materialisation. Signing
// recanonicalises deterministically (it does not compare arrival bytes), so a
// signed Data simply materialises its value on verify — a legitimate touch,
// unchanged by lazy. Nested signed Data round-trips through the lean
// envelope-recognition, no longer through a key-shape sniff.
//
// Signing is "for free" via the signing module: RunAction<sign>/<verify> against
// the actor context, whose identity is auto-provisioned — no key plumbing.
public class Cut3_SignThenWireThenVerify
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
        => _app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-cut3-" + System.Guid.NewGuid().ToString("N")[..8]));

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.System.Context;

    private async Task<data> Sign(data d)
        => await _app.RunAction<sign>(new sign { Context = Ctx, Data = d }, Ctx);

    private async Task<data> Verify(data d)
        => await _app.RunAction<verify>(new verify { Context = Ctx, Data = d }, Ctx);

    // Per-actor serializer is context-bound, so a round-tripped Data lands with
    // its Signature ([In]-inflow) intact.
    private data RoundTrip(data d)
    {
        var s = _app.System.Channel.Serializers.GetByMimeType("application/plang");
        return (data)s.Deserialize(s.Serialize(d).Value!).Value!;
    }

    [Test] public async Task Cut3_SignedData_VerifiesAgainstRaw_WithoutMaterialising()
    {
        var signed = await Sign(new data("msg", "hello"));
        await signed.IsSuccess();
        await Assert.That(signed.Signature).IsNotNull();

        var back = RoundTrip(signed);                 // through the wire
        var ok = await Verify(back);                   // recanonicalise + check
        await ok.IsSuccess();
        await Assert.That(ok.GetValue<bool>()).IsTrue();
    }

    // The case the lean envelope-recognition covers: a signed Data nested in a
    // value slot round-trips and its inner signature still reaches verify.
    [Test] public async Task Cut3_NestedSignedData_InnerSignatureReachesVerify()
    {
        var inner = await Sign(new data("inner", "secret"));
        await Assert.That(inner.Signature).IsNotNull();

        var outer = new data("outer", inner);          // value IS the signed inner Data
        var back = RoundTrip(outer);

        var innerBack = back.Value as data;            // rehydrated nested Data
        await Assert.That(innerBack).IsNotNull();
        await Assert.That(innerBack!.Signature).IsNotNull().Because("nested signature survived the wire");
        var ok = await Verify(innerBack!);
        await ok.IsSuccess();
        await Assert.That(ok.GetValue<bool>()).IsTrue();
    }

    // Negative — a tampered value fails verification.
    [Test] public async Task Cut3_TamperedRaw_FailsVerification()
    {
        var signed = await Sign(new data("msg", "hello"));
        var back = RoundTrip(signed);
        back.Value = "tampered";                        // mutate after signing

        var ok = await Verify(back);
        await Assert.That(ok.Success && ok.Value is true).IsFalse();
    }
}
