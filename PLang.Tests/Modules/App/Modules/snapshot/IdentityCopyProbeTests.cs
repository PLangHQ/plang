using Identity = global::app.module.identity.Identity;

namespace PLang.Tests.App.Modules.snapshot;

// Security probes (snapshot 309), real signer and the real identity store: an identity record a program holds is a copy.
// Resume trusts "one of the app's non-archived identities", so a copy that leaked back would let a program re-trust a retired key.
[NotInParallel]
public class IdentityCopyProbeTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_probe_idcopy_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_dir);
    }

    [After(Test)]
    public void Teardown()
    {
        try { System.IO.Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private global::app.@this App() => new global::app.@this(_dir, global::PLang.Tests.Parent.Actor).Consenting();

    // a snapshot of the app standing in goal G's second step, signed as the app signs now
    private static async Task<global::app.data.@this> Saved(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        Step? second = null;
        foreach (var (index, line) in new[] { (0, "variable.set(Name=%s0%, Value=\"first\")"), (1, "variable.set(Name=%s1%, Value=\"second\")"),
                     (2, "goal.return(Data={\"s1\": %s1%})") })
        {
            var step = new Step(app.type.list) { Goal = goal, Index = index, Text = line };
            step.Code.Add((await context.Action(line)).In(step));
            goal.Step.Add(step);
            if (index == 1) second = step;
        }
        app.goal.list.Add(goal);
        await using var running = context.call.Push(second!.Goal!, await context.call.Setting());
        await using var frame = context.call.Push(second!, await context.call.Setting());
        await using var call = context.call.Push(second!.Code[0], await context.call.Setting(), context.Variable);
        var saved = await new global::app.module.snapshot.save(context).Start();
        await call.DisposeAsync(); await frame.DisposeAsync(); await running.DisposeAsync();
        return saved;
    }

    private static async Task<global::app.data.@this> Resume(global::app.data.@this snap, global::app.actor.context.@this user)
    {
        await user.Variable.Set("snap", snap);
        return await (await user.Action("snapshot.resume(Snapshot=%snap%)")).Start(user);
    }

    // A: hold the default identity, flip every settable member on the copy; the snapshot it signed still resumes, the registry's
    // record is unchanged, and the app signs with the same key afterwards.
    [Test]
    public async Task A_AHeldDefaultIdentity_FlippedToArchived_DoesNotChangeTheAppsTrust()
    {
        await using var app = App();
        var system = app.actor.list.System.Context;
        var user = app.actor.list.User.Context;
        var snap = await Saved(app);
        var signedBy = snap.Signature!.Identity.ToString();

        var get = new global::app.module.identity.Get(system);
        await get.Attach(null, system);
        var held = await get.Start();
        await held.IsSuccess();
        var keyBefore = ((Identity)(await held.Value())!).PublicKey;
        await user.Variable.Set("id", held);
        foreach (var edit in new[] { "isArchived", "isDefault", "publicKey", "name" })
            await (await user.Action($"variable.set(Name=%id.{edit}%, Value={(edit is "isArchived" ? "true" : edit is "isDefault" ? "false" : "\"tampered\"")})")).Start(user);

        var resumed = await Resume(snap, user);
        var again = new global::app.module.identity.Get(system);
        await again.Attach(null, system);
        var registry = (Identity)(await (await again.Start()).Value())!;
        var next = await Saved(app);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDA resume={resumed.Success}/{resumed.Error?.Key} registry.archived={registry.IsArchived} registry.key.same={registry.PublicKey == keyBefore} nextSignedBy.same={next.Signature!.Identity.ToString() == signedBy}");
        await Assert.That(resumed.Success).IsTrue();
        await Assert.That(registry.IsArchived).IsFalse();
        await Assert.That(registry.PublicKey).IsEqualTo(keyBefore);
        await Assert.That(next.Signature!.Identity.ToString()).IsEqualTo(signedBy);
    }

    // B: archive an identity through the real door, hold the copy it answers, set isArchived false, resume a snapshot it
    // signed: still refused.
    [Test]
    public async Task B_AHeldArchivedIdentity_FlippedToNotArchived_IsStillRefused()
    {
        await using var app = App();
        var system = app.actor.list.System.Context;
        var user = app.actor.list.User.Context;

        var old = new global::app.module.identity.Create(system) { Name = (global::app.type.item.text.@this)"old", Default = (global::app.type.item.@bool.@this)true };
        await old.Attach(null, system);
        await (await old.Start()).IsSuccess();
        var snap = await Saved(app);

        var next = new global::app.module.identity.Create(system) { Name = (global::app.type.item.text.@this)"next", Default = (global::app.type.item.@bool.@this)true };
        await next.Attach(null, system);
        await (await next.Start()).IsSuccess();
        var archive = new global::app.module.identity.Archive(system) { Name = (global::app.type.item.text.@this)"old" };
        await archive.Attach(null, system);
        var archived = await archive.Start();
        await archived.IsSuccess();

        var before = await Resume(snap, user);
        await user.Variable.Set("id", archived);
        await (await user.Action("variable.set(Name=%id.isArchived%, Value=false)")).Start(user);
        var after = await Resume(snap, user);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"IDB before={before.Error?.Key} after={after.Error?.Key} held.archived={((Identity)(await archived.Value())!).IsArchived}");
        await Assert.That(before.Error?.Key).IsEqualTo("SnapshotNotTheApps");
        await Assert.That(after.Error?.Key).IsEqualTo("SnapshotNotTheApps");
    }
}
