using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

// The strict path also fires at runtime when `%var%` deferred from build resolves
// to a concrete value. Mismatch → typed runtime error. Match → mint normally.

public class StrictRunTests
{
    [Test] public async Task Run_StrictImageGifWithRuntimeVarResolvingToPng_ThrowsTypedError()
    {
        // `set %img% = %upload% as image/gif strict` where %upload% resolves to PNG bytes.
        // ValidateBuild returned null (var ref); Run sees the concrete value, calls
        // ValidateKind, fails, throws a typed runtime error (ServiceError or equivalent).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Run_StrictImageGifWithRuntimeVarResolvingToGif_Mints()
    {
        // Same setup; %upload% resolves to GIF bytes; ValidateKind ok; mint as
        // image, Type carries {name:image, kind:gif, strict:true}.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Run_NotStrict_StampsKindFromBuildHook_NoValidation()
    {
        // `set %x% = "readme.md" as text` — not strict. Run mints with
        // Type.Kind == "md" (from text.Build), no ValidateKind call.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
