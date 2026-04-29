namespace PLang.Tests.Generator;

// Contract tests for the v4 source generator simplification.
// Big picture (per Ingi): Data flows through; no unwrapping; generator is simpler.
//
// v4 contract:
//   - Every action property is Data<T> or [Provider]. Raw scalars (`partial string Path`) are a build error.
//   - Generator emits ONE shape per parameter property (uniform across all kinds — collapses today's 11-arm dispatch).
//   - Generated ExecuteAsync is thin: marker init, eager [Provider] resolution, backing-field reset, validation, return Run().
//     Scaffolding (callstack, save/restore, try/catch/finally) lives in App.Run, NOT in generated code.
//   - Property hierarchy: ActionProperty (abstract) → DataProperty + ProviderProperty. Two leaves.
//
// Some tests inspect generator output via Roslyn (compile a tiny test source, read generated trees);
// others inspect the live PLang.dll's generated types via reflection.

public class GeneratorValidationTests
{
    // Source compile: handler with `partial string Path` → build fails with "must be Data<T> or [Provider]" error.
    [Test] public async Task RawScalarProperty_FailsBuild_WithSpecificError() => Assert.Fail("Not implemented");

    // Source compile: handler with `partial Data<string> Path` → builds successfully.
    [Test] public async Task DataTProperty_BuildsSuccessfully() => Assert.Fail("Not implemented");

    // Source compile: handler with `[Provider] partial IFoo Foo` → builds successfully.
    [Test] public async Task ProviderProperty_BuildsSuccessfully() => Assert.Fail("Not implemented");

    // Build error includes property name and class name so the author can find the offender.
    [Test] public async Task BuildError_IncludesPropertyAndClassNames() => Assert.Fail("Not implemented");

    // Generated property body is byte-identical for Data<string> and Data<int> (modulo the type token) —
    //   verifies the "one uniform shape" promise.
    [Test] public async Task GeneratedPropertyShape_UniformAcrossDataTtypes() => Assert.Fail("Not implemented");

    // Generated property body uses __action.GetParameter(name, Context).As<T>(Context) — the v4 lookup-then-resolve idiom.
    [Test] public async Task GeneratedPropertyBody_UsesGetParameterAndAsT() => Assert.Fail("Not implemented");

    // Generator stops emitting __Resolve, __ResolveData, __StripPercent, __TryConvert, __FormatValue, __HasParam — none in any output.
    [Test] public async Task Generator_DoesNotEmitOldHelperFamily() => Assert.Fail("Not implemented");

    // Generator stops emitting data.ResetResolution() in parameter-Data construction.
    [Test] public async Task Generator_DoesNotEmitResetResolution() => Assert.Fail("Not implemented");

    // Generated ExecuteAsync contains NO try/catch/finally — scaffolding is App.Run's job.
    [Test] public async Task GeneratedExecuteAsync_HasNoTryCatchFinally() => Assert.Fail("Not implemented");

    // Generated ExecuteAsync calls Run() directly (no helper wrapper) — verifies the "thin" body.
    [Test] public async Task GeneratedExecuteAsync_CallsRunDirectly() => Assert.Fail("Not implemented");

    // Generator project structure: PLang.Generators/this.cs is orchestration only (under ~80 lines).
    [Test] public async Task GeneratorOrchestration_IsLightweight() => Assert.Fail("Not implemented");

    // Property hierarchy: only DataProperty and ProviderProperty exist as concrete leaves under Emission/Property/.
    [Test] public async Task PropertyHierarchy_TwoLeavesOnly() => Assert.Fail("Not implemented");

    // ActionProperty record uses value-equal primitive fields (no IPropertySymbol leaks) — Roslyn incremental safety.
    [Test] public async Task ActionPropertyRecord_NoSymbolLeaks_IncrementalSafe() => Assert.Fail("Not implemented");
}
