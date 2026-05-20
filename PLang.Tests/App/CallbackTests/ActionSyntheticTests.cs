using TUnit.Core;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 3: `Action.@this.Synthetic` defaults to true for inline-C#
/// constructed actions; the source generator emits `Synthetic = false` for
/// PR-built actions. `CallStack.Push` reads it; wire-serialize filters by it.
public class ActionSyntheticTests
{
    [Test] public Task Synthetic_DefaultsToTrue_OnInlineCSharpConstruction()    { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Synthetic_SourceGenEmits_FalseFor_PrBuiltAction()        { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task CallStackPush_StampsSynthetic_OnCallFrame()              { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task SnapshotWireSerializer_DropsSyntheticFrames()            { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task InMemorySnapshot_KeepsSyntheticFrames_ForDebugTelemetry(){ Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
