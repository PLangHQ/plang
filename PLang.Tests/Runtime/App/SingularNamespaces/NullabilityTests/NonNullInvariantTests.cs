using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.NullabilityTests;

// Batch E — Stage 2 nullability contract (architect spec).
//
// Production producers always stamp Context downstream of mint (Variables.Set,
// Action.RunAsync, snapshot restore); a type's class is answered by the registry
// (`app.Type`), never by a static no-context lookup.
//
// The 5 structural back-refs (step.Goal, channel.Actor, channel.Channels and
// the App back-refs on module/goal/error) are flipped non-null.
public class NonNullInvariantTests
{
    [Test] public async Task ClrType_OnUnstampedDomainType_ReturnsNull()
    {
        // A bare type answers only the class it was born with — a non-primitive name born
        // without one answers null; the registry answers by name for a caller with a context.
        var t = new global::app.type.@this("not-a-primitive-domain-name");
        await Assert.That(t.ClrType).IsNull();
    }


    [Test] public async Task DataType_OnStampedData_ResolvesDomainType_ViaRegistry_NotStaticFallback()
    {
        // `path` lives only in the per-App catalog, so resolving its ClrType proves the
        // read went through Context.App.Type.Clr.
        await using var app = new PLangEngine("/test");

        // A bare type object answers only the class stamped at its birth; the asker brings the
        // context and the registry answers by name.
        var d = new global::app.data.@this("", "any/raw/value",
            new global::app.type.@this("path"), context: app.User.Context);
        await Assert.That(d.Type.ClrType).IsNull()
            .Because("a bare type holds no context — it never reaches the registry itself.");
        var clr = d.Context!.App.Type.Clr(d.Type.Name);
        await Assert.That(clr).IsNotNull()
            .Because("registry knows 'path' → typeof(global::app.type.item.path.@this); static fallback returns null.");
        await Assert.That(clr!.Name).IsEqualTo("this")
            .Because("the registered CLR type for 'path' is app.type.item.path.@this — Type.Name strips the @-escape.");
        await Assert.That(clr!.Namespace).IsEqualTo("app.type.item.path");
    }

    [Test] public async Task AppParent_OnRootApp_IsNull_ByDesign()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Parent).IsNull();
    }

    [Test] public async Task StepGoal_OnOwnedStep_IsNonNull_AfterBackRefFlip()
    {
        var stepGoalProp = typeof(global::app.goal.step.@this).GetProperty("Goal");
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
