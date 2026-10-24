using app.module.identity;

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
        _app = new global::app.@this(_root, global::PLang.Tests.Parent.Actor).TestSigning();
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
        _app.actor.list.User.Identity = new Identity("caller", _app.type.list[typeof(global::app.module.identity.Identity)]) { PublicKey = "ab+c/d==" };

        var identity = await _app.actor.list.User.Context.Variable.Get("Identity");

        await Assert.That((await identity.Value())?.ToString()).IsEqualTo("ab-c_d");
    }

    // An actor born after the app bound something to a variable's set (a service actor, spawned later) holds %Identity%
    // and %MyIdentity% the moment it is handed out: they are seeded with the memory, as %Now% is, not set — and a set
    // that waits (a before-event held open here) cannot leave it without them.
    [Test]
    public async Task ALaterBornActor_HasItsIdentityVariablesAtOnce_WhateverIsBoundToSet()
    {
        var gate = new TaskCompletionSource();
        _app.variable.type.HeldIn(_app).Bind("set", global::app.@event.When.before, async (_, _, ctx) => { await gate.Task; return ctx.Ok(); },
            _app.actor.list.User, global::app.@event.binding.Scope.app);

        await using var born = new global::app.actor.@this("Service", _app, fallback: _app.actor.list.System);

        await Assert.That(born.Context.Variable.Contains("Identity")).IsTrue();
        await Assert.That(born.Context.Variable.Contains("MyIdentity")).IsTrue();
        gate.SetResult();
    }

    // Seeding is not a set: what is bound to set does not fire for a variable the memory is born holding.
    [Test]
    public async Task TheIdentityVariables_AreSeeded_SoNoSetEventFiresForThem()
    {
        var fired = new List<string>();
        foreach (var when in new[] { global::app.@event.When.before, global::app.@event.When.after })
            _app.variable.type.HeldIn(_app).Bind("set", when,
                (item, _, ctx) => { fired.Add(((global::app.type.item.variable.@this)item).Name); return Task.FromResult(ctx.Ok()); },
                _app.actor.list.User, global::app.@event.binding.Scope.app);

        await using var born = new global::app.actor.@this("Service", _app, fallback: _app.actor.list.System);

        await Assert.That(fired).DoesNotContain("Identity");
        await Assert.That(fired).DoesNotContain("MyIdentity");
    }

    // A later-born actor acts for the identity it is given, and %MyIdentity% is still the app's own — read as before.
    [Test]
    public async Task ALaterBornActor_ActsForItsIdentity_AndMyIdentityIsTheApps()
    {
        await using var born = new global::app.actor.@this("Service", _app, fallback: _app.actor.list.System);
        born.Identity = new Identity("caller", _app.type.list[typeof(global::app.module.identity.Identity)]) { PublicKey = "ab+c/d==" };
        var mine = (await (await _app.actor.list.System.Context.Variable.Get("MyIdentity")).Value()) as Identity;

        var identity = await born.Context.Variable.Get("Identity");
        var apps = await born.Context.Variable.Get("MyIdentity");

        await Assert.That((await identity.Value())?.ToString()).IsEqualTo("ab-c_d");
        await Assert.That(((await apps.Value()) as Identity)?.PublicKey).IsEqualTo(mine!.PublicKey);
    }
}
