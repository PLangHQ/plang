using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — parameter resolution at the DISPATCH boundary (the settled model;
// supersedes the lazy-GetParameter draft this file originally pinned). The
// generated `__ResolveParameters()` — awaited by ExecuteAsync/SetAction before
// Run()/Build() — decodes each .pr parameter's %var%/literal form once per
// execution into the handler's backing field: the handler instance is the
// per-execution home, the shared .pr parameter is never written to, and the
// property getter is a plain backing read. `await Param.Value()` stays the one
// read surface; content (file/url) still loads through the value's own door.
public class Stage2_GetParameterLazyTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang", "app")))
                dir = Directory.GetParent(dir)?.FullName;
            return dir ?? throw new InvalidOperationException("repo root not found");
        }
    }

    private static string ReadGenerated(string handlerName)
    {
        var generatedDir = Path.Combine(RepoRoot, "PLang.Tests", "Generator", "obj", "Debug", "net10.0",
            "generated", "PLang.Generators", "PLang.Generators.this");
        return File.ReadAllText(Path.Combine(generatedDir, handlerName));
    }

    [Test]
    public async Task DispatchResolution_HandlerSeesResolvedValue_SharedPrParamUntouched()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<global::app.module.matrix.resolution.FullVarMatch>(app,
            parameters: new[] { ("path", (object?)"%path%") },
            variables: new Dictionary<string, object?> { ["path"] = "/tmp/x.txt" });

        await result.Data.IsSuccess();
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.Clr<string>()).IsEqualTo("/tmp/x.txt");
    }

    [Test]
    public async Task ResolutionFailure_SurfacesAsTypedError_AtValueDoor()
    {
        // An unconvertible literal for a typed slot surfaces as a typed FromError Data
        // when the value is materialised at its door — never an NRE inside Run().
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<global::app.module.matrix.plain.IntPlain>(app,
            parameters: new[] { ("count", (object?)"not-a-number") });
        var typed = result.Data as global::app.data.@this<global::app.type.number.@this>;
        await typed!.Value();
        await result.Data.IsFailure();
    }

    [Test]
    public async Task BadScheme_ResolvedDataReturnsTypedError_NotNreOnValueBang()
    {
        // file.read with an unregistered scheme: the path conversion fails as a typed
        // error Data (SchemeNotRegistered), surfaced by the post-resolve guard — no NRE.
        await using var app = TestApp.Create("/app");
        var context = app.User.Context;
        var slot = new Data("path", "s3://bucket/key", context: context);
        var failedPath = slot.ShallowClone<global::app.type.path.@this>(await slot.Value<global::app.type.path.@this>());
        await failedPath.IsFailure();
        await Assert.That(failedPath.Error!.Key).IsEqualTo("SchemeNotRegistered");
    }

    [Test]
    public async Task RepresentativeHandler_AwaitGuardUse_Shape()
    {
        // file/read.cs: value read via `await Path.Value()`, the resolution-error guard
        // `if (!Path.Success)` AFTER the await, use after the guard.
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot, "PLang", "app", "module", "file", "read.cs"));
        var awaitIdx = src.IndexOf("await Path.Value()", StringComparison.Ordinal);
        var guardIdx = src.IndexOf("if (!Path.Success)", StringComparison.Ordinal);
        await Assert.That(awaitIdx >= 0).IsTrue();
        await Assert.That(guardIdx > awaitIdx).IsTrue();
    }
}
