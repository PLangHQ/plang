namespace PLang.Tests.App.Modules.identity;

// The app's first identity is made once. Its own row is signed on the way into the store, and that signing
// asks for the identity — which must answer with the one being stored, not make another.
public class FirstIdentityTests
{
    [Test] public async Task TheFirstIdentity_IsMadeOnce_ItsOwnSaveDoesntMakeAnother()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-first-id-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            // real signing and the real identity provider — the loop lives between them
            await using var app = new global::app.@this(root).Consenting();
            var ctx = app.actor.list.User.Context;

            var gets = 0;
            foreach (var who in new[] { app.actor.list.User, app.actor.list.System })
                app.@event.Add(app.type.list["action"]).Add("start")[global::app.@event.When.before].Add(side => new global::app.@event.binding.@this(side, (item, _, c) =>
                {
                    if (item is global::app.goal.step.action.@this { Name: "get" } a && a.Module.Name == "identity")
                        System.Threading.Interlocked.Increment(ref gets);
                    return Task.FromResult(c.Ok());
                }, who, global::app.@event.binding.Scope.actor, global::app.@event.binding.@this.Always));

            var identity = await new global::app.goal.step.action.composed.@this(new global::app.module.identity.Get(ctx), ctx).Start(ctx);
            await identity.IsSuccess();

            // the one ask, and the one its own save makes — never a chain down to the stack's limit
            await Assert.That(gets).IsLessThanOrEqualTo(4);
            await Assert.That((await app.actor.list.System.Setting.Get<global::app.module.identity.setting.@this>()).Identity.CountRaw).IsEqualTo(1);
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    // Of first asks at once, one makes the identity and the others wait and answer it: one identity, the one stored.
    [Test] public async Task FirstAsksAtOnce_AnswerOneIdentity_TheOneStored()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-first-id-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var app = new global::app.@this(root).Consenting();
            var ctx = app.actor.list.User.Context;
            var (provider, _) = app.Code.Get<global::app.module.identity.code.IIdentity>();

            var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
                provider!.GetOrCreateDefaultAsync(new global::app.module.identity.Get(ctx)))));

            var keys = new List<string?>();
            foreach (var answer in answers) { await answer.IsSuccess(); keys.Add((await answer.Value())?.PublicKey); }
            await Assert.That(keys.Distinct().Count()).IsEqualTo(1);
            var stored = (await app.actor.list.System.Setting.Get<global::app.module.identity.setting.@this>()).Identity;
            await Assert.That(stored.CountRaw).IsEqualTo(1);
            await Assert.That((await stored.Items(ctx).Single().Value<global::app.module.identity.Identity>())?.PublicKey).IsEqualTo(keys[0]);
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    // Two first saves at once are each signed by the app's one identity, so each is a snapshot this app saved: a resume
    // takes both past the signature (each holds no call, so each answers that there is no frame to go on from).
    [Test] public async Task FirstSavesAtOnce_BothResume()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-first-id-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var app = new global::app.@this(root).Consenting();
            var ctx = app.actor.list.User.Context;

            var saves = await Task.WhenAll(Task.Run(() => app.Snapshot(ctx)), Task.Run(() => app.Snapshot(ctx)));

            foreach (var save in saves)
            {
                await save.IsSuccess();
                await ctx.Variable.Set("snap", save);
                var resumed = await (await ctx.Action("snapshot.resume(Snapshot=%snap%)")).Start(ctx);
                await Assert.That(resumed.Error?.Key).IsEqualTo("NoFrameToResume");
            }
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }
}
