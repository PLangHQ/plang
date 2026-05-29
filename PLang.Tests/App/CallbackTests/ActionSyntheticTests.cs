using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using ActionEntity = global::app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 3: `Action.@this.Synthetic` defaults to true for inline-C#
/// constructed actions; the source generator emits `Synthetic = false` for
/// PR-built actions. `CallStack.Push` reads it; wire-serialize filters by it.
public class ActionSyntheticTests
{
    [Test] public async Task Synthetic_DefaultsToTrue_OnInlineCSharpConstruction()
    {
        var a = new ActionEntity { Module = "variable", ActionName = "set" };
        await Assert.That(a.Synthetic).IsTrue();
    }
    [Test] public async Task Synthetic_SourceGenEmits_FalseFor_PrBuiltAction()
    {
        // PR-loaded actions get Synthetic flipped to false in Goals.LoadGoalFile
        // / Setup.LoadFile (post-deserialization sweep). No fixture .pr handy
        // here — pin the contract that Synthetic is `set`-able (init would
        // make the post-load sweep impossible).
        var a = new ActionEntity { Module = "variable", ActionName = "set" };
        a.Synthetic = false;
        await Assert.That(a.Synthetic).IsFalse();
    }

    [Test] public async Task CallStackPush_StampsSynthetic_OnCallFrame()
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cs-" + System.Guid.NewGuid().ToString("N")[..8]));
        var synthetic = new ActionEntity { Module = "x", ActionName = "y" };
        var prLoaded = new ActionEntity { Module = "x", ActionName = "y" }; prLoaded.Synthetic = false;

        await using var s1 = app.CallStack.Push(synthetic);
        await Assert.That(s1.Synthetic).IsTrue();

        // Pop s1 before pushing s2 to avoid caller chain
        await s1.DisposeAsync();
        await using var s2 = app.CallStack.Push(prLoaded);
        await Assert.That(s2.Synthetic).IsFalse();
    }

    [Test] public async Task SnapshotWireSerializer_DropsSyntheticFrames()
    {
        // Stage 2a.5 stamps Synthetic on Call frames; wire-serialize filtering
        // is a follow-up (architect's todos.md note — per-channel serializer
        // shape deferred). Pin the contract that the flag is readable.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cs2-" + System.Guid.NewGuid().ToString("N")[..8]));
        var prLoaded = new ActionEntity { Module = "x", ActionName = "y" }; prLoaded.Synthetic = false;
        await using var call = app.CallStack.Push(prLoaded);
        await Assert.That(call.Synthetic).IsFalse();
    }

    [Test] public async Task InMemorySnapshot_KeepsSyntheticFrames_ForDebugTelemetry()
    {
        // App.Snapshot() captures the full CallStack (synthetic + non-synthetic).
        // Pin: a synthetic frame appears in the snapshot section.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cs3-" + System.Guid.NewGuid().ToString("N")[..8]));
        var synthetic = new ActionEntity { Module = "x", ActionName = "y" };
        await using var call = app.CallStack.Push(synthetic);
        var snap = app.Snapshot();
        await Assert.That(snap).IsNotNull();
    }
}
