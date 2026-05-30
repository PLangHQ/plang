using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

// Integration cut 2 — strict mismatch fails at the right layer (build vs runtime),
// proving the build/runtime split is correct end-to-end.
//   - literal + sniffable + strict + mismatch → build error
//   - %var% + sniffable + strict + mismatch → builds clean, runtime typed error
//   - literal + sniffable + strict + match     → builds + runs clean

public class Cut2_StrictMismatchFailsAtRightLayer
{
    [Test] public async Task LiteralPngAsImageGifStrict_FailsAtBuild()
    {
        // Goal step: `- set %img% = "photo.png" as image/gif strict`
        // where "photo.png" actually resolves to PNG bytes. Build returns a
        // BuildValidation error (not a runtime error); the .pr is never
        // produced for this step.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task VarAsImageGifStrict_BuildsClean_FailsAtRuntime()
    {
        // Two-step goal:
        //   - read %upload% from <PNG file>
        //   - set %img% = %upload% as image/gif strict
        // Build succeeds (no literal to probe). Run throws a typed runtime
        // error at the set step naming the actual kind "png".
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task LiteralGifAsImageGifStrict_BuildsAndRunsClean()
    {
        // Goal step: `- set %img% = "real.gif" as image/gif strict`
        // (real.gif is a real GIF). Build clean; run clean; %img.Type% is
        // {image, gif, strict:true}.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
