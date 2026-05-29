using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.NullabilityTests;

// Batch E — Stage 2: app and context are non-null invariants.
// The ?. defensiveness is gone; an un-stamped data.Type read throws (the bug the ?. used to hide).
// Static fallbacks (GetPrimitiveOrMime, GetTypeNameStatic external sites) are gone.
// 5 structural back-refs (step→Goal, channel→Actor/Channels, channels→Actor) are non-null.
// app.Parent stays nullable (root has no parent — the one legitimate nullable).
public class NonNullInvariantTests
{
    // Cut 4 — the headline invariant payoff.
    [Test] public async Task DataType_OnUnstampedData_ThrowsHard_NoSilentFallback()
        => Assert.Fail("Not implemented");

    // Stamped data resolves a primitive via the registry, NOT via the static GetPrimitiveOrMime branch.
    [Test] public async Task DataType_OnStampedData_ResolvesPrimitive_WithoutStaticFallback()
        => Assert.Fail("Not implemented");

    // Probe: the four `?? GetPrimitiveOrMime` external call sites are gone (reflection / source scan).
    [Test] public async Task GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved()
        => Assert.Fail("Not implemented");

    // Probe: the three `App?.Types... ?? GetTypeNameStatic` external fallbacks are gone.
    [Test] public async Task GetTypeNameStatic_ExternalFallbackCallSites_AllRemoved()
        => Assert.Fail("Not implemented");

    // app.Parent stays nullable — the root app has no parent. The one legitimate nullable on app.
    [Test] public async Task AppParent_OnRootApp_IsNull_ByDesign()
        => Assert.Fail("Not implemented");

    // Spot check on a structural back-ref: a step's Goal is non-null once the step is owned.
    [Test] public async Task StepGoal_OnOwnedStep_IsNonNull_AfterBackRefFlip()
        => Assert.Fail("Not implemented");

    // Spot check: a registered channel's Actor and Channels back-refs are non-null.
    [Test] public async Task ChannelActorAndChannelsBackRefs_OnRegisteredChannel_AreNonNull()
        => Assert.Fail("Not implemented");
}
