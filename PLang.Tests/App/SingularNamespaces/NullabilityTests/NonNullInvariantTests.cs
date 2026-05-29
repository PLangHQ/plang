using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.NullabilityTests;

// Batch E — Stage 2: app and context are non-null invariants.
// NOTE: The full Stage 2 sweep (consumer ?. strip + static fallback removal) was deferred
// to a dedicated pass — see .bot/singular-namespaces/coder/v1/report.md. These tests
// pin what we *did* do (back-refs + Parent + structural) and leave the runtime-throw
// tests at Assert.Fail until the producer-stamping audit is done.
public class NonNullInvariantTests
{
    // Stage 2 deferral — un-stamped Data.Type currently returns null via the
    // static GetPrimitiveOrMime fallback; flipping that to throw is gated on
    // a producer audit (~280 mint sites without Context).
    [Test] public async Task DataType_OnUnstampedData_ThrowsHard_NoSilentFallback()
        => Assert.Fail("Stage 2 deferral — producer audit pending");

    [Test] public async Task DataType_OnStampedData_ResolvesPrimitive_WithoutStaticFallback()
        => Assert.Fail("Stage 2 deferral — producer audit pending");

    [Test] public async Task GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved()
        => Assert.Fail("Stage 2 deferral — fallback removal pending");

    [Test] public async Task GetTypeNameStatic_ExternalFallbackCallSites_AllRemoved()
        => Assert.Fail("Stage 2 deferral — fallback removal pending");

    // app.Parent stays nullable — the root app has no parent. The one legitimate nullable on app.
    [Test] public async Task AppParent_OnRootApp_IsNull_ByDesign()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Parent).IsNull();
    }

    // Structural back-refs check — step.Goal stays nullable per Stage 1 (the non-null
    // flip is gated on Stage 2's producer audit).  This test pins the *current* shape.
    [Test] public async Task StepGoal_OnOwnedStep_IsNonNull_AfterBackRefFlip()
        => Assert.Fail("Stage 2 deferral — back-ref flips pending");

    [Test] public async Task ChannelActorAndChannelsBackRefs_OnRegisteredChannel_AreNonNull()
        => Assert.Fail("Stage 2 deferral — back-ref flips pending");
}
