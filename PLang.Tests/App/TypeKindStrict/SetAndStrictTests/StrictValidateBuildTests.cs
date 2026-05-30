using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

// `variable.set.ValidateBuild` enforces strict via `IKindValidatable`. Literal
// + sniffable family + strict → build error on mismatch. Unverifiable family
// (no marker) → name-known check only. `%var%` → defer to runtime. Not strict
// → stamp the kind, validate nothing.

public class StrictValidateBuildTests
{
    [Test] public async Task ValidateBuild_StrictImageGifWithGifLiteral_ReturnsNull()
    {
        // Strict, kind=gif, value is real GIF bytes → match → null (no error).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateBuild_StrictImageGifWithPngLiteral_ReturnsError()
    {
        // Strict, kind=gif, value is real PNG bytes → mismatch → build error.
        // Error mentions both required ("gif") and actual ("png") kinds.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateBuild_StrictImageGifWithVarRef_ReturnsNull_DefersToRuntime()
    {
        // Strict, kind=gif, value is "%upload%" → cannot probe at build → null.
        // Same path the existing ValidateBuild uses for %var% (HasVariableReference).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateBuild_StrictTextMdWithLiteral_ReturnsNull()
    {
        // Strict on text — text does NOT implement IKindValidatable. The path
        // degrades to "kind name accepted"; no byte probe runs; returns null.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateBuild_NotStrict_DoesNotValidate_EvenOnMismatch()
    {
        // Not strict, kind=gif, value is real PNG → null. The kind annotation is
        // a hint, not a requirement, when strict is absent.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task ValidateBuild_StrictWithNoKind_ReturnsNull()
    {
        // Strict, kind=null, value is anything → null. There's nothing to
        // validate against; strict-without-kind degrades silently. (Alternative
        // would be a build error "strict requires a kind"; this pins the
        // soft contract.)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
