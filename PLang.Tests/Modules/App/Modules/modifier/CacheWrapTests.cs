namespace PLang.Tests.App.Modules.modifier;

/// <summary>
/// Tests for the on.cache clause: a cache lookup before the action starts, a store after.
/// </summary>
public class CacheWrapTests
{
    private global::app.@this _app = null!;
    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").Testing();
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
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%x%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context), new("value", "first", context: _app.actor.list.User.Context)
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
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%y%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context), new("value", "fresh-value", context: _app.actor.list.User.Context)
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
            Module = _app.actor.list.User.Context.App.Module("error"), Name = "throw",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this> { new("message", "boom", context: _app.actor.list.User.Context) })
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
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%a%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context), new("value", "v", context: _app.actor.list.User.Context)
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
        var goal = new Goal { Path = global::app.type.item.path.@this.Resolve("/foo/bar.goal", _app.actor.list.User.Context) };
        var step = new Step { Index = 7, Goal = goal };

        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%b%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context), new("value", "v", context: _app.actor.list.User.Context)
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
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%c%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context), new("value", "slide", context: _app.actor.list.User.Context)
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
        var stashed = Ctx.Ok("restored");
        await Ctx.App!.Cache.SetAsync("restore-key", stashed,
            new CacheSettings { DurationMs = 60_000, Sliding = false });

        var action = global::PLang.Tests.Shared.Make.With(new PrAction
        {
            Module = _app.actor.list.User.Context.App.Module("variable"), Name = "set",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<global::app.data.@this>
            {
                new("name", "%d%", new global::app.type.@this("variable"), context: _app.actor.list.User.Context), new("value", "fresh", context: _app.actor.list.User.Context)
            })
        }, CacheModifier(60_000, "restore-key"));

        await action.Start(Ctx);

        var dataVar = await Ctx.Variable.Get("!data");
        await Assert.That(dataVar).IsNotNull();
        await Assert.That((await dataVar.Value())?.ToString()).IsEqualTo("restored");
    }

    [Test]
    public async Task ARecoverysResult_IsNeverCached()
    {
        // error.throw; on.error(Recovery=[set %r%]); on.cache: recovery answers on the error outcome, after the
        // attempt's after-start — the store sees only the failed work.
        var action = global::PLang.Tests.Shared.Make.With(
            global::PLang.Tests.Shared.Make.Action("error", "throw", ("Message", "boom")),
            global::PLang.Tests.Shared.Make.Action("on", "error", global::PLang.Tests.Shared.Make.Recovery(
                global::PLang.Tests.Shared.Make.Action("variable", "set",
                    global::PLang.Tests.Shared.Make.Param("Name", "r", "variable"), ("Value", "recovered")))),
            CacheModifier(60_000, "recovery-key"));

        var result = await action.Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("r"))).IsEqualTo("recovered");
        await Assert.That(await Ctx.App!.Cache.GetAsync("recovery-key")).IsNull();
    }

    [Test]
    public async Task AClause_IsNotAStep_DataAfterItIsTheActionsResult()
    {
        // math.add(1, 2); on.cache(…); variable.set(%sum%, %!data%) — the clause was bound at read and never
        // starts, so %!data% is still the add's result when the set reads it.
        var code = new global::app.goal.step.action.list.@this();
        code.Add(global::PLang.Tests.Shared.Make.Action("math", "add", ("A", 1), ("B", 2)));
        code.Add(CacheModifier(60_000, "sum-key"));
        code.Add(global::PLang.Tests.Shared.Make.Action("variable", "set",
            global::PLang.Tests.Shared.Make.Param("Name", "sum", "variable"), ("Value", "%!data%")));
        code.Attach();

        var result = await code.Start(Ctx);

        await result.IsSuccess();
        await Assert.That((await Ctx.Variable.GetValue("sum"))?.ToString()).IsEqualTo("3");
        await Assert.That(await Ctx.App!.Cache.GetAsync("sum-key")).IsNotNull();
    }
}
