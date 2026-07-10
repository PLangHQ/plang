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
    [Test] public async Task ClrType_OnUnstampedDomainType_ReturnsNull()
    {
        // ClrType has its own resolver chain (_clrType ?? Context?... ?? GetPrimitiveOrMime)
        // — it does NOT go through Promote().  For a non-primitive name with no Context,
        // the chain falls off the end and returns null.  This pins the ClrType behaviour
        // alone; the fail-loud Promote() contract is pinned by the next two tests.
        // Pin the type entity's ClrType resolver alone — no value (a number value
        // would, correctly, win and report its own Int32: the instance IS the value).
        var t = new global::app.type.@this("not-a-primitive-domain-name");
        await Assert.That(t.ClrType).IsNull();
    }

    [Test] public async Task TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard()
    {
        // Promote() guards Fields/Values/Properties/Shape/ConstructorSignature/Example/
        // Description/Kinds — every schema-fold property routes through it.  An entity
        // minted via FromName (no Context, no fold data) hitting any of these has to
        // throw, not silently return null — silent null would surface as wrong LLM
        // prompts far from the producer-stamping bug.
        var t = new global::app.type.@this("not-a-primitive-domain-name"); // no Context

        await Assert.That(() => { _ = t.Fields; return Task.CompletedTask; })
            .Throws<System.InvalidOperationException>()
            .Because("Promote() must fail-loud when an unstamped non-primitive entity is read.");
    }

    [Test] public async Task TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext()
    {
        // The primitive-fallback path (the 2-arg ctor with a CLR type) marks
        // _foldLoaded=true at construction, so primitives stay reachable through
        // `app.Type["string"].Example` without an App stamped — that no-Context
        // identity surface is the documented carve-out the throw above is built
        // around.  This deletion-confirms: remove the `_foldLoaded = true` line in
        // the 2-arg ctor and this test goes red.
        await using var app = new PLangEngine("/test");
        var prim = app.Type["string"]; // primitive path: new type("string", typeof(string))
        await Assert.That(() => { _ = prim.Example; return Task.CompletedTask; })
            .ThrowsNothing();
    }

    [Test] public async Task DataType_OnStampedData_ResolvesDomainType_ViaRegistry_NotStaticFallback()
    {
        // Asserting on `int` would be meaningless — both the registry and the
        // static GetPrimitiveOrMime path return typeof(int) for "int", so the
        // assertion couldn't distinguish them.  Use a domain type registered
        // only in the per-App catalog: `path` lives in the registry, has no
        // entry in the static primitive/MIME table, so resolving its ClrType
        // proves the read went through Context.App.Type.Clr — not the static
        // fallback (which would return null).
        await using var app = new PLangEngine("/test");
        var staticAnswer = global::app.type.catalog.@this.GetPrimitiveOrMime("path");
        await Assert.That(staticAnswer).IsNull()
            .Because("guard: if the static path ever learns about 'path', this test no longer proves what its name claims.");

        var d = new global::app.data.@this("", "any/raw/value",
            new global::app.type.@this("path"), context: app.User.Context);
        await Assert.That(d.Type.ClrType).IsNotNull()
            .Because("registry knows 'path' → typeof(global::app.type.item.path.@this); static fallback returns null.");
        await Assert.That(d.Type.ClrType!.Name).IsEqualTo("this")
            .Because("the registered CLR type for 'path' is app.type.item.path.@this — Type.Name strips the @-escape.");
        await Assert.That(d.Type.ClrType!.Namespace).IsEqualTo("app.type.item.path");
    }

    [Test] public async Task GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved()
    {
        // The `Context?.X ?? GetPrimitiveOrMime(...)` chain at consumer sites is gone.
        // Static GetPrimitiveOrMime stays on type.catalog.@this as the no-context surface,
        // and type.@this still uses it as its OWN entity-level fallback for primitives;
        // what's removed is consumer sites chaining it as `?? fallback` after a context
        // lookup of their own.
        var sources = new[] {
            "PLang/app/data/this.cs",
            "PLang/app/module/variable/set.cs",
        };
        var repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        foreach (var rel in sources)
        {
            var path = System.IO.Path.Combine(repo, rel);
            // Fail loud if the guard file is missing — otherwise a CI layout change
            // would silently skip both sources and the test would pass vacuously.
            await Assert.That(System.IO.File.Exists(path)).IsTrue()
                .Because($"guard file {rel} not found at {path} — relative-walk from BaseDirectory broke.");
            var text = await System.IO.File.ReadAllTextAsync(path);
            await Assert.That(text.Contains("?? AppTypes.GetPrimitiveOrMime") || text.Contains("?? global::app.type.catalog.@this.GetPrimitiveOrMime"))
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
        var t = global::app.type.catalog.@this.GetTypeNameStatic(typeof(int));
        // Post-Stage-2: typeof(int) canonicalises to "number".
        await Assert.That(t).IsEqualTo("number");
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
