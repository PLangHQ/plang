namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 3
// Per-type reconstruction hook. Some types can't be populated by setting public properties:
// path is the canonical case — abstract, no parameterless ctor, needs Context to wire FileSystem.
// The hook mechanism is generic; coder picks (interface? attribute? naming convention?).
// For path specifically, the hook reads the Relative field from the normalized tree and calls
// path.Resolve(relative, ctx) — yielding the scheme-correct subclass.

public class AsReconstructionHookTests
{
    [Test] public async Task HookDiscovery_FindsPath_HookOverridesGenericPropertyBagPath()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_Path_FromNormalizedTree_CallsPathResolve_WithRelative()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_FilePath_RoundTrips_Through_PathResolve_File()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_HttpPath_RoundTrips_Through_PathResolve_Http()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task As_Path_RequiresContextArgument_ThrowsWhenAbsent()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task HookCache_Populated_HookLookupHits_OnSecondCallSameType()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task PathJsonConverter_Read_Deleted_Or_DelegatesToAsPathHook()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
