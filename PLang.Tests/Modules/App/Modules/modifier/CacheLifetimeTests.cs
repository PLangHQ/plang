namespace PLang.Tests.App.Modules.modifier;

/// <summary>An app's step cache lives as long as its app: disposed with it, what it held is let go — never kept alive
/// by its expiry timers past an app that is gone.</summary>
public class CacheLifetimeTests
{
    [Test]
    public async Task DisposingAnApp_DisposesItsStepCache()
    {
        var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await app.Cache.SetAsync("kept", ctx.Ok("a value"), new CacheSettings { DurationMs = 60_000, Sliding = false });
        await Assert.That(await app.Cache.GetAsync("kept")).IsNotNull();

        await app.DisposeAsync();

        await Assert.That(await app.Cache.GetAsync("kept")).IsNull()
            .Because("the cache went with its app: nothing it held is kept for the rest of the minute");
    }
}
