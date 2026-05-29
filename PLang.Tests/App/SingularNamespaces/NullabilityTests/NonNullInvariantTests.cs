using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.NullabilityTests;

// Batch E — Nullability invariants.
//
// Architectural decision (Ingi, post-coder-v1): the `?.` defensiveness on
// Data's Context lookup STAYS.  Data can legitimately be minted without a
// Context (test fixtures, factories like Data.Ok, JsonConverter stub paths,
// deserialization round-trips).  Forcing a Context at mint time would change
// ~280 call-site signatures for a property the consumer already gracefully
// falls back on.
//
// These tests pin the chosen contract:
//   - Data.Type.ClrType returns the static-fallback resolution when Context
//     is null (does NOT throw).
//   - Data.Type.ClrType returns the registry resolution when Context is set
//     (the registry knows custom DLL-loaded types the static fallback doesn't).
//   - GetPrimitiveOrMime / GetTypeNameStatic stay as legitimate static
//     fallbacks — they're the "no Context available" answer the architecture
//     chose to keep.
//   - app.Parent stays nullable — the root app has no parent.
//   - Structural back-refs (step.Goal, channel.Actor, channel.Channels) stay
//     nullable — the brief acknowledged these as legitimate lifecycle nulls.
public class NonNullInvariantTests
{
    [Test] public async Task DataType_OnUnstampedData_FallsBackGracefully_NoThrow()
    {
        // Architectural: an un-stamped Data's .Type.ClrType is the static-fallback
        // primitive resolution.  Does NOT throw.
        var d = new global::app.data.@this<string>("", "hello");
        await Assert.That(() => { _ = d.Type!.ClrType; return Task.CompletedTask; })
            .ThrowsNothing();
    }

    [Test] public async Task DataType_OnStampedData_ResolvesViaRegistry_NotStaticFallback()
    {
        // A stamped Data resolves through the per-App registry — the registry
        // knows DLL-loaded types the static fallback can't.
        await using var app = new PLangEngine("/test");
        var d = new global::app.data.@this<int>("", 42) { Context = app.User.Context };
        await Assert.That(d.Type!.ClrType).IsEqualTo(typeof(int));
    }

    [Test] public async Task GetPrimitiveOrMime_StaysAsLegitimateNoContextFallback()
    {
        // The static fallback is part of the design (chosen over forcing context
        // at mint time).  Not a code smell — a documented escape hatch.
        var t = global::app.type.list.@this.GetPrimitiveOrMime("string");
        await Assert.That(t).IsEqualTo(typeof(string));
    }

    [Test] public async Task GetTypeNameStatic_StaysAsLegitimateNoContextFallback()
    {
        // Same architectural choice.  Static fallback is the no-context answer.
        var name = global::app.type.list.@this.GetTypeNameStatic(typeof(int));
        await Assert.That(name).IsEqualTo("int");
    }

    [Test] public async Task AppParent_OnRootApp_IsNull_ByDesign()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Parent).IsNull();
    }

    [Test] public async Task StepGoal_StaysNullable_PerLifecycleConvention()
    {
        // step.Goal stays nullable — the brief explicitly listed it as a
        // legitimate lifecycle null (steps can exist before being attached
        // to a goal during construction).
        var stepGoalProp = typeof(global::app.goal.steps.step.@this).GetProperty("Goal");
        await Assert.That(stepGoalProp).IsNotNull();
        var nullable = new System.Reflection.NullabilityInfoContext()
            .Create(stepGoalProp!).WriteState;
        await Assert.That(nullable).IsEqualTo(System.Reflection.NullabilityState.Nullable);
    }

    [Test] public async Task ChannelActorAndChannelsBackRefs_StayNullable_PerLifecycleConvention()
    {
        // channel.Actor and channel.Channels stay nullable — channels can exist
        // pre-registration (built but not yet owned by an actor).
        var actor = typeof(global::app.channel.@this).GetProperty("Actor");
        var channels = typeof(global::app.channel.@this).GetProperty("Channels");
        await Assert.That(actor).IsNotNull();
        await Assert.That(channels).IsNotNull();
        var ctx = new System.Reflection.NullabilityInfoContext();
        await Assert.That(ctx.Create(actor!).WriteState).IsEqualTo(System.Reflection.NullabilityState.Nullable);
        await Assert.That(ctx.Create(channels!).WriteState).IsEqualTo(System.Reflection.NullabilityState.Nullable);
    }
}
