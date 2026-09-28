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
        var typed = result.Data as global::app.data.@this<global::app.type.item.text.@this>;
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
        var typed = result.Data as global::app.data.@this<global::app.type.item.number.@this>;
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
        var failedPath = slot.As<global::app.type.item.path.@this>(await slot.Value<global::app.type.item.path.@this>());
        await failedPath.IsFailure();
        await Assert.That(failedPath.Error!.Key).IsEqualTo("SchemeNotRegistered");
    }

    [Test]
    public async Task Use_OnACarrierThatDidNotResolve_AnswersItsOwnError_ContinuationNeverRuns()
    {
        // A handler hands a carrier's value on through Use; a carrier whose resolution failed
        // answers itself — its typed error — and what it would have been handed to never runs.
        await using var app = TestApp.Create("/app");
        var context = app.User.Context;
        var slot = new Data("path", "s3://bucket/key", context: context);
        var failedPath = slot.As<global::app.type.item.path.@this>(await slot.Value<global::app.type.item.path.@this>());
        var ran = false;

        var answer = await failedPath.Use(_ => { ran = true; return Task.FromResult(context.Ok()); });

        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("SchemeNotRegistered");
        await Assert.That(ran).IsFalse();
    }

    [Test]
    public async Task Use_OnAResolvedCarrier_HandsItsValueWhole()
    {
        await using var app = TestApp.Create("/app");
        var context = app.User.Context;
        var slot = new Data("path", "/tmp/x.txt", context: context);
        var carrier = slot.As<global::app.type.item.path.@this>(await slot.Value<global::app.type.item.path.@this>());
        global::app.type.item.path.@this? handed = null;

        var answer = await carrier.Use(p => { handed = p; return Task.FromResult(context.Ok()); });

        await answer.IsSuccess();
        await Assert.That(handed).IsSameReferenceAs(await carrier.Value());
    }
}
