namespace PLang.Tests.App.Modules.modifier;

/// <summary>An app's cache lives as long as its app: disposed with it, what it held is let go — never kept alive by its
/// expiry timers past an app that is gone (a server's app per request would keep every request's entries).</summary>
public class CacheLifetimeTests
{
    [Test]
    public async Task AnAppsCache_LetsItsEntriesGo_WhenTheAppIsDisposed()
    {
        var app = global::PLang.Tests.Session.App("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await app.Cache.SetAsync("kept", ctx.Ok("a value"), new global::app.goal.step.CacheSettings { DurationMs = 60_000, Sliding = false });
        await Assert.That(await app.Cache.GetAsync("kept")).IsNotNull();

        await app.DisposeAsync();

        await Assert.That(await app.Cache.GetAsync("kept")).IsNull()
            .Because("the cache went with its app: nothing it held is kept for the rest of the minute");
    }
}
