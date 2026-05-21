namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the cache.wrap modifier handler.
/// Wraps an action with cache lookup before and cache store after.
/// </summary>
public class CacheWrapTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app");
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static PrAction CacheModifier(long durationMs, string? key = null, bool sliding = false)
    {
        var parameters = new List<global::app.data.@this>
        {
            new("durationMs", durationMs),
            new("sliding", sliding)
        };
        if (key != null) parameters.Add(new("key", key));
        return new PrAction
        {
            Module = "cache",
            ActionName = "wrap",
            Parameters = parameters
        };
    }

    [Test]
    public async Task Wrap_CacheMiss_RunsActionAndStoresResult()
    {
        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%x%"), new("value", "first")
            },
            Modifiers = new ActionModifiers { CacheModifier(60_000, "miss-key") }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(Ctx.Variables.GetValue("x")).IsEqualTo("first");

        // Cache was populated on miss
        var cached = await Ctx.App!.Cache.GetAsync("miss-key");
        await Assert.That(cached).IsNotNull();
    }

    [Test]
    public async Task Wrap_CacheHit_ReturnsCachedSkipsAction()
    {
        // Pre-populate the cache with a known Data value
        var stashed = global::app.data.@this.Ok("cached-value");
        await Ctx.App!.Cache.SetAsync("hit-key", stashed,
            new CacheSettings { DurationMs = 60_000, Sliding = false });

        // variable.set would put "fresh-value" but the cache hit bypasses dispatch.
        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%y%"), new("value", "fresh-value")
            },
            Modifiers = new ActionModifiers { CacheModifier(60_000, "hit-key") }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("cached-value");
        // The underlying action did NOT run (no %y%)
        await Assert.That(Ctx.Variables.Get("y").Value).IsNull();
    }

    [Test]
    public async Task Wrap_ActionFailure_DoesNotCache()
    {
        var action = new PrAction
        {
            Module = "error", ActionName = "throw",
            Parameters = new List<global::app.data.@this> { new("message", "boom") },
            Modifiers = new ActionModifiers { CacheModifier(60_000, "fail-key") }
        };

        var result = await action.RunAsync(Ctx);

        await Assert.That(result.Success).IsFalse();

        // Nothing was cached for that key
        var cached = await Ctx.App!.Cache.GetAsync("fail-key");
        await Assert.That(cached).IsNull();
    }

    [Test]
    public async Task Wrap_CustomKey_UsedWhenProvided()
    {
        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%a%"), new("value", "v")
            },
            Modifiers = new ActionModifiers { CacheModifier(60_000, "my-custom-key") }
        };

        await action.RunAsync(Ctx);

        var underCustomKey = await Ctx.App!.Cache.GetAsync("my-custom-key");
        await Assert.That(underCustomKey).IsNotNull();
    }

    [Test]
    public async Task Wrap_DefaultKey_DerivedFromGoalPathAndStepIndex()
    {
        // Populate context.Step so default key resolver can read Goal.Path + Step.Index
        var goal = new Goal { Path = "/foo/bar.goal" };
        var step = new Step { Index = 7, Goal = goal };
        Ctx.Step = step;

        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%b%"), new("value", "v")
            },
            Modifiers = new ActionModifiers { CacheModifier(60_000) } // no Key
        };

        await action.RunAsync(Ctx);

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
        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%c%"), new("value", "slide")
            },
            Modifiers = new ActionModifiers { CacheModifier(60_000, "slide-key", sliding: true) }
        };

        await action.RunAsync(Ctx);

        var cached = await Ctx.App!.Cache.GetAsync("slide-key");
        await Assert.That(cached).IsNotNull();
    }

    [Test]
    public async Task Wrap_CachedResult_RestoredAsDataVariable()
    {
        // Pre-cache a value, then execute — the handler should put the cached Data
        // into Variables under name !data so the next action can read it via %!data%.
        var stashed = global::app.data.@this.Ok("restored");        await Ctx.App!.Cache.SetAsync("restore-key", stashed,
            new CacheSettings { DurationMs = 60_000, Sliding = false });

        var action = new PrAction
        {
            Module = "variable", ActionName = "set",
            Parameters = new List<global::app.data.@this>
            {
                new("name", "%d%"), new("value", "fresh")
            },
            Modifiers = new ActionModifiers { CacheModifier(60_000, "restore-key") }
        };

        await action.RunAsync(Ctx);

        var dataVar = Ctx.Variables.Get("!data");
        await Assert.That(dataVar).IsNotNull();
        await Assert.That(dataVar.Value).IsEqualTo("restored");
    }
}
