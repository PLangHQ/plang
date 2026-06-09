namespace PLang.Tests.App.CompareRedesign;

// Stage 2 — `Action.GetParameter<T>(name)` returns a lazy `Data<T>`. The getter
// does NOT navigate or read — it wraps the param `%var%` and sets context.
// Resolution + the content read fire only at `await Param.Value()`. The
// resolution-error guard moves *after* the await: ~42 sites migrate as
// `var p = await Param.Value(); if (!Param.Success) return Param; … p`.
// A pre-await guard would silently inspect the unresolved Data and miss errors.
public class Stage2_GetParameterLazyTests
{
    [Test]
    public async Task GetParameterT_Returns_LazyDataT_GetterDoesNotRead()
    {
        // MaterializeCount=0 after `var p = action.GetParameter<text>("x")` — getter is sync, cheap
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task AwaitParamValue_TriggersResolutionAndRead()
    {
        // await p.Value() → MaterializeCount=1, returns the typed value (text/number/...)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ParamResolutionGuard_FiresAfterAwait_NotBefore()
    {
        // pre-await `if (!p.Success)` inspects unresolved Data → false-negative;
        // the canonical pattern is `var v = await p.Value(); if (!p.Success) return p; ... v`
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task BadScheme_ResolvedDataReturnsTypedError_NotNreOnValueBang()
    {
        // a bad-scheme / unset %var% / convert failure surfaces as a typed Data error,
        // never an NRE on `.Value!` post-await
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ResolveDataWrapper_Removed_AsTImpl_FoldedIntoValueAsync()
    {
        // reflection on generator output: no `__ResolveData<T>` wrapper emitted —
        // `AsT_Impl` resolution body moved into the async `.Value()`
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetParameterT_Generic_ReplacesNonGenericGetter()
    {
        // the non-generic `GetParameter(string, context)` (action/this.cs:220) is no longer the public path;
        // `GetParameter<T>` collapses `GetParameter(name, ctx).As<T>(Context)` into one typed call
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task FortyTwoSiteMigration_AwaitGuardUsePattern_Verified_OnRepresentativeHandler()
    {
        // pick one migrated handler (e.g. file/read) and verify the await→guard→use shape:
        // 1) the param is read via `await Param.Value()` 2) the guard `if (!Param.Success)` lives AFTER 3) `p` is used after the guard
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
