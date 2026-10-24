namespace PLang.Tests.App.Core;

/// <summary>A request app leaves nothing behind: made, run on a small goal and disposed, it is collected — its step
/// cache with it. A webserver makes one per request, so whatever outlives one outlives thousands.</summary>
public class RequestAppsLeaveNothingBehindTests
{
    // A request: an app born, a goal with a cached step run on it, the app disposed. Only weak references come out.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<(System.WeakReference app, System.WeakReference cache)> Request()
    {
        var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var set = global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(ctx, "Name", "%answer%", "variable"), ("Value", "kept for a minute"));
        var cached = global::PLang.Tests.Shared.Make.Action(ctx, "on", "cache",
            ("Duration", System.TimeSpan.FromMinutes(1)), ("Key", "request"));
        var goal = global::PLang.Tests.Shared.Make.Goal(ctx, "Request",
            global::PLang.Tests.Shared.Make.Step("set %answer%, cache for a minute", set, cached));

        await (await goal.Start(ctx)).IsSuccess();
        await Assert.That(await app.Cache.GetAsync("request")).IsNotNull();

        await app.DisposeAsync();
        return (new System.WeakReference(app), new System.WeakReference(app.Cache));
    }

    [Test]
    public async Task ARequestApp_IsCollected_ItsCacheWithIt()
    {
        var (app, cache) = await Request();

        for (var i = 0; i < 3 && (app.IsAlive || cache.IsAlive); i++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        await Assert.That(cache.IsAlive).IsFalse().Because("the step cache went with its app: no expiry timer keeps it");
        await Assert.That(app.IsAlive).IsFalse().Because("nothing a request made keeps its app after it is disposed");
    }
}
