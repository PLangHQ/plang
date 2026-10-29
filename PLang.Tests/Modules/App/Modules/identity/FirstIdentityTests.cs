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
                await who.@event.Add(new global::app.@event.code.@this(app.type.list["action"], "start", global::app.@event.When.before, (item, _, c) =>
                {
                    if (item is global::app.goal.step.action.@this { Name: "get" } a && a.Module.Name == "identity")
                        System.Threading.Interlocked.Increment(ref gets);
                    return Task.FromResult(c.Ok());
                }, who, global::app.@event.code.@this.Always));

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

    // The app's identities change one change at a time, each reading the list inside: creates at once each keep their
    // row, archives at once each land, renames at once leave every new name and no old one.
    [Test] public async Task IdentityChangesAtOnce_EachKeepsTheOthers()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-first-id-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var app = new global::app.@this(root).Consenting();
            var ctx = app.actor.list.User.Context;
            var names = Enumerable.Range(0, 8).Select(i => $"id{i}").ToArray();
            async Task<global::app.data.@this> Run(string line) => await (await ctx.Action(line)).Start(ctx);
            async Task<List<global::app.module.identity.Identity>> Stored()
            {
                var rows = (await app.actor.list.System.Setting.Get<global::app.module.identity.setting.@this>()).Identity;
                var all = new List<global::app.module.identity.Identity>();
                foreach (var row in rows.Items(ctx)) all.Add((await row.Value<global::app.module.identity.Identity>())!);
                return all;
            }

            // the app's default made first, so none of these is it (the default isn't archived without force)
            await (await Run("identity.get()")).IsSuccess();
            foreach (var created in await Task.WhenAll(names.Select(n => Task.Run(() => Run($"identity.create(Name=\"{n}\")")))))
                await created.IsSuccess();
            await Assert.That((await Stored()).Count(i => names.Contains(i.Name))).IsEqualTo(names.Length);

            foreach (var archived in await Task.WhenAll(names.Take(4).Select(n => Task.Run(() => Run($"identity.archive(Name=\"{n}\")")))))
                await archived.IsSuccess();
            await Assert.That((await Stored()).Count(i => names.Take(4).Contains(i.Name) && i.IsArchived)).IsEqualTo(4);

            foreach (var renamed in await Task.WhenAll(names.Skip(4).Select(n => Task.Run(() => Run($"identity.rename(Name=\"{n}\", NewName=\"{n}x\")")))))
                await renamed.IsSuccess();
            var after = await Stored();
            await Assert.That(after.Count(i => names.Skip(4).Select(n => n + "x").Contains(i.Name))).IsEqualTo(4);
            await Assert.That(after.Count(i => names.Skip(4).Contains(i.Name))).IsEqualTo(0);
            await Assert.That(after.Count(i => names.Take(4).Contains(i.Name) && i.IsArchived)).IsEqualTo(4);
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    // An archive and a set-default of one identity at once: whichever goes first, the other is refused — archived, it
    // can't be made the default; the default, it isn't archived without force. The default is never archived.
    [Test] public async Task ArchiveAndSetDefaultAtOnce_OneLands_TheDefaultIsNeverArchived()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-first-id-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var app = new global::app.@this(root).Consenting();
            var ctx = app.actor.list.User.Context;
            async Task<global::app.data.@this> Run(string line) => await (await ctx.Action(line)).Start(ctx);

            await (await Run("identity.get()")).IsSuccess();
            for (var round = 0; round < 12; round++)
            {
                var name = $"id{round}";
                await (await Run($"identity.create(Name=\"{name}\")")).IsSuccess();
                var answers = await Task.WhenAll(
                    Task.Run(() => Run($"identity.archive(Name=\"{name}\")")),
                    Task.Run(() => Run($"identity.setDefault(Name=\"{name}\")")));

                await Assert.That(answers.Count(a => a.Success)).IsEqualTo(1);
                var rows = (await app.actor.list.System.Setting.Get<global::app.module.identity.setting.@this>()).Identity;
                foreach (var row in rows.Items(ctx))
                    await Assert.That((await row.Value<global::app.module.identity.Identity>()) is { IsDefault: true, IsArchived: true }).IsFalse();
            }
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
