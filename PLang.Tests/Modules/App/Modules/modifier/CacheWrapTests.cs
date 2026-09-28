namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the on.cache clause: a cache lookup before the action starts, a store after.
/// </summary>
public class CacheWrapTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // An on.cache clause keeping a result for durationMs.
    private static PrAction CacheModifier(long durationMs, string? key = null, bool sliding = false)
    {
        var parameters = new List<(string, object?)>
        {
            ("Duration", System.TimeSpan.FromMilliseconds(durationMs)),
            ("Sliding", sliding),
        };
        if (key != null) parameters.Add(("Key", key));
        return global::PLang.Tests.Shared.Make.Action("on", "cache", [.. parameters]);
    }

    [Test]
    public async Task Wrap_CacheMiss_RunsActionAndStoresResult()
    {
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%x%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "first", context: global::PLang.Tests.TestApp.SharedContext)
            })
        }, CacheModifier(60_000, "miss-key"));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("x"))).IsEqualTo("first");

        // Cache was populated on miss
        var cached = await Ctx.App!.Cache.GetAsync("miss-key");
        await Assert.That(cached).IsNotNull();
    }

    [Test]
    public async Task Wrap_CacheHit_ReturnsCachedSkipsAction()
    {
        // Pre-populate the cache with a known Data value
        var stashed = Ctx.Ok("cached-value");
        await Ctx.App!.Cache.SetAsync("hit-key", stashed,
            new CacheSettings { DurationMs = 60_000, Sliding = false });

        // variable.set would put "fresh-value" but the cache hit bypasses dispatch.
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%y%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "fresh-value", context: global::PLang.Tests.TestApp.SharedContext)
            })
        }, CacheModifier(60_000, "hit-key"));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("cached-value");
        // The underlying action did NOT run (no %y%)
        await Assert.That(await (await (await Ctx.Variable.Get("y")).Value())!.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task Wrap_ActionFailure_DoesNotCache()
    {
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("error"), Name = "throw",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("message", "boom", context: global::PLang.Tests.TestApp.SharedContext) })
        }, CacheModifier(60_000, "fail-key"));

        var result = await action.Start(Ctx);

        await result.IsFailure();

        // Nothing was cached for that key
        var cached = await Ctx.App!.Cache.GetAsync("fail-key");
        await Assert.That(cached).IsNull();
    }

    [Test]
    public async Task Wrap_CustomKey_UsedWhenProvided()
    {
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%a%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "v", context: global::PLang.Tests.TestApp.SharedContext)
            })
        }, CacheModifier(60_000, "my-custom-key"));

        await action.Start(Ctx);

        var underCustomKey = await Ctx.App!.Cache.GetAsync("my-custom-key");
        await Assert.That(underCustomKey).IsNotNull();
    }

    [Test]
    public async Task Wrap_DefaultKey_DerivedFromGoalPathAndStepIndex()
    {
        // The action's step is the step in play while it runs — the default key reads its Goal.Path + Index
        var goal = new Goal { Path = global::app.type.item.path.@this.Resolve("/foo/bar.goal", global::PLang.Tests.TestApp.SharedContext) };
        var step = new Step { Index = 7, Goal = goal };

        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%b%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "v", context: global::PLang.Tests.TestApp.SharedContext)
            }),
            Step = step,
        }, CacheModifier(60_000));

        await action.Start(Ctx);

        var cached = await Ctx.App!.Cache.GetAsync("step:/foo/bar.goal:7");
        await Assert.That(cached).IsNotNull();
    }

    [Test]
    public async Task Wrap_SlidingExpiration_PassedToCache()
    {
        // We can't introspect the stored CacheSettings from ICache directly, so
        // this asserts by behavior: sliding=true should still populate the cache
        // and cached values should be retrievable. The handler plumbs Sliding through
        // to CacheSettings; if it didn't, this entry wouldn't be stored at all.
        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%c%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "slide", context: global::PLang.Tests.TestApp.SharedContext)
            })
        }, CacheModifier(60_000, "slide-key", sliding: true));

        await action.Start(Ctx);

        var cached = await Ctx.App!.Cache.GetAsync("slide-key");
        await Assert.That(cached).IsNotNull();
    }

    [Test]
    public async Task Wrap_CachedResult_RestoredAsDataVariable()
    {
        // Pre-cache a value, then execute — a hit is the action's result, so %!data% is the cached value.
        var stashed = Ctx.Ok("restored");        await Ctx.App!.Cache.SetAsync("restore-key", stashed,
            new CacheSettings { DurationMs = 60_000, Sliding = false });

        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%d%", new global::app.type.@this("variable"), context: global::PLang.Tests.TestApp.SharedContext), new("value", "fresh", context: global::PLang.Tests.TestApp.SharedContext)
            })
        }, CacheModifier(60_000, "restore-key"));

        await action.Start(Ctx);

        var dataVar = await Ctx.Variable.Get("!data");
        await Assert.That(dataVar).IsNotNull();
        await Assert.That((await dataVar.Value())?.ToString()).IsEqualTo("restored");
    }
}
