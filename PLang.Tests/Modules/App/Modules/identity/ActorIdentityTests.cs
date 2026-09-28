using app.module.action.identity;

namespace PLang.Tests.App.Modules.identity;

/// <summary>
/// %Identity% is who an actor acts for: its identity's public key — a caller's, in a service — and, in a local
/// run, the system's own (%MyIdentity%). A public key is written URL-safe, so %Identity% is a path segment as it is.
/// </summary>
public class ActorIdentityTests : System.IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-identity-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this _app;

    public ActorIdentityTests()
    {
        System.IO.Directory.CreateDirectory(_root);
        _app = TestApp.Plain(_root);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        try { System.IO.Directory.Delete(_root, true); } catch (System.IO.IOException) { }
    }

    [Test]
    public async Task InALocalRun_TheUserActsAsTheSystem()
    {
        var mine = (await (await _app.actor.list.System.Context.Variable.Get("MyIdentity")).Value()) as Identity;

        var identity = await _app.actor.list.User.Context.Variable.Get("Identity");

        await Assert.That(mine).IsNotNull();
        await Assert.That((await identity.Value())?.ToString()).IsEqualTo(mine!.PublicKey);
        await Assert.That(mine.PublicKey.IndexOfAny(['+', '/', '='])).IsEqualTo(-1);
    }

    [Test]
    public async Task AnActorWithAnIdentity_ActsForIt_ItsKeyReadUrlSafe()
    {
        _app.actor.list.User.Identity = new Identity("caller") { PublicKey = "ab+c/d==" };

        var identity = await _app.actor.list.User.Context.Variable.Get("Identity");

        await Assert.That((await identity.Value())?.ToString()).IsEqualTo("ab-c_d");
    }
}
