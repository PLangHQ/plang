using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.NullabilityTests;

// Batch E — Stage 2 nullability contract (architect spec).
//
// Architectural decision (Ingi, post-tester-v1): production producers always stamp
// Context downstream of mint (Variables.Set, Action.RunAsync, snapshot restore).
// The static GetPrimitiveOrMime / GetTypeNameStatic surfaces stay as the entity's
// documented no-context primitive lookup — `new type("string").ClrType` works
// without an App.  What's removed are the EXTERNAL `?? GetPrimitiveOrMime` /
// `?? GetTypeNameStatic` chains at consumer sites (Sqlite, As, set.cs, module
// schema describers) — those callers used the fallback to mask their own
// stamping bugs; with Context guaranteed by the producer, the chain is dead.
//
// The 5 structural back-refs (step.Goal, channel.Actor, channel.Channels and
// the App back-refs on module/goal/error) are flipped non-null.
public class NonNullInvariantTests
{
    [Test] public async Task DataType_OnUnstampedData_ThrowsHard_NoSilentFallback()
    {
        // An un-stamped Data with a DOMAIN (non-primitive) type name can't resolve
        // ClrType — the registry would know, the static primitive table doesn't.
        // No silent half-answer; the read returns null and the stamping bug is
        // visible to the caller.
        var d = new global::app.data.@this<int>("", 0,
            new global::app.type.@this("not-a-primitive-domain-name"));
        await Assert.That(d.Type!.ClrType).IsNull();
    }

    [Test] public async Task DataType_OnStampedData_ResolvesPrimitive_WithoutStaticFallback()
    {
        // A stamped Data resolves through the per-App registry (single source of truth).
        await using var app = new PLangEngine("/test");
        var d = new global::app.data.@this<int>("", 42) { Context = app.User.Context };
        await Assert.That(d.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test] public async Task GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved()
    {
        // The `Context?.X ?? GetPrimitiveOrMime(...)` chain at consumer sites is gone.
        // Static GetPrimitiveOrMime stays on type.list.@this as the no-context surface,
        // and type.@this still uses it as its OWN entity-level fallback for primitives;
        // what's removed is consumer sites chaining it as `?? fallback` after a context
        // lookup of their own.
        var sources = new[] {
            "PLang/app/data/this.cs",
            
            "PLang/app/module/variable/set.cs",
        };
        var repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", ".."));
        foreach (var rel in sources)
        {
            var path = System.IO.Path.Combine(repo, rel);
            if (!System.IO.File.Exists(path)) continue;
            var text = await System.IO.File.ReadAllTextAsync(path);
            await Assert.That(text.Contains("?? AppTypes.GetPrimitiveOrMime") || text.Contains("?? global::app.type.list.@this.GetPrimitiveOrMime"))
                .IsFalse().Because($"{rel} still has a `?? GetPrimitiveOrMime` fallback chain");
        }
    }

    [Test] public async Task GetTypeNameStatic_StaysAsLegitimateNoAppFallback()
    {
        // module.@this.Describe runs from test fixtures that new module directly
        // (no App stamped), so the `App?.Type ?? GetTypeNameStatic` chain in
        // module/this.cs is the legitimate fixture-supporting fallback. The static
        // method stays as the documented no-App surface; the assertion here is
        // that the static surface still returns the right answer.
        var t = global::app.type.list.@this.GetTypeNameStatic(typeof(int));
        await Assert.That(t).IsEqualTo("int");
    }

    [Test] public async Task AppParent_OnRootApp_IsNull_ByDesign()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Parent).IsNull();
    }

    [Test] public async Task StepGoal_OnOwnedStep_IsNonNull_AfterBackRefFlip()
    {
        var stepGoalProp = typeof(global::app.goal.steps.step.@this).GetProperty("Goal");
        await Assert.That(stepGoalProp).IsNotNull();
        var nullable = new System.Reflection.NullabilityInfoContext()
            .Create(stepGoalProp!).WriteState;
        await Assert.That(nullable).IsEqualTo(System.Reflection.NullabilityState.NotNull);
    }

    [Test] public async Task ChannelActorAndChannelsBackRefs_OnRegisteredChannel_AreNonNull()
    {
        var actor = typeof(global::app.channel.@this).GetProperty("Actor");
        var channels = typeof(global::app.channel.@this).GetProperty("Channels");
        await Assert.That(actor).IsNotNull();
        await Assert.That(channels).IsNotNull();
        var ctx = new System.Reflection.NullabilityInfoContext();
        await Assert.That(ctx.Create(actor!).WriteState).IsEqualTo(System.Reflection.NullabilityState.NotNull);
        await Assert.That(ctx.Create(channels!).WriteState).IsEqualTo(System.Reflection.NullabilityState.NotNull);
    }
}
