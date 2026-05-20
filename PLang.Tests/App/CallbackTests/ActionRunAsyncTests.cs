using TUnit.Core;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: action owns its execution. `action.RunAsync(ctx)` is
/// the single entry; `App.Run` and `App.RunAction` are deleted. Static-survey
/// tests pin that the dead symbols are gone from production source.
public class ActionRunAsyncTests
{
    [Test] public Task ActionRunAsync_IsSingleEntry_PushAnchorExecute()         { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task AppRun_SymbolAbsent_FromProductionSource()               { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task AppRunAction_SymbolAbsent_FromProductionSource()         { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task CauseParameter_AbsentFromAllCallSites()                  { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
