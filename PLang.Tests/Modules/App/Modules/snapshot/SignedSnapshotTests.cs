using Identity = global::app.module.identity.Identity;

namespace PLang.Tests.App.Modules.snapshot;

/// <summary>
/// A snapshot is signed by the app that saved it, in its own view, and a run goes on only from one this app signed as
/// it is: carried in a .plang file or a db row alike, read at rest (no window, no replay), and refused when it is
/// unsigned, changed, or signed by an identity that is not one of the app's own.
/// </summary>
public class SignedSnapshotTests
{
    // An app that signs for real (ed25519), its identities those the provider given answers (the shared test keypair by
    // default, so two apps made so are the same signer).
    private static global::app.@this App(global::app.module.identity.code.IIdentity? identities = null)
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-signed-" + System.Guid.NewGuid().ToString("N")[..8]),
            global::PLang.Tests.Parent.Actor);
        app.test.list.Open(app.actor.list.User);
        app.Code.Register(identities ?? new global::PLang.Tests.Shared.TestIdentity { IsBuiltIn = true });
        app.Code.SetDefault<global::app.module.identity.code.IIdentity>(identities?.Name ?? "test-identity");
        return app;
    }

    // Goal G: two steps, each setting a variable, then a step answering what the run saw (a resumed run's memory is its
    // own app's); the same goal (and hash) in every app that holds it.
    private static async Task<Step> Holds(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        Step? second = null;
        foreach (var (index, line) in new[] { (0, "variable.set(Name=%s0%, Value=\"first\")"), (1, "variable.set(Name=%s1%, Value=\"second\")"),
                     (2, "goal.return(Data={\"s1\": %s1%, \"note\": %note%})") })
        {
            var step = new Step(app.type.list) { Goal = goal, Index = index, Text = line };
            step.Code.Add((await context.Action(line)).In(step));
            goal.Step.Add(step);
            if (index == 1) second = step;
        }
        app.goal.list.Add(goal);
        return second!;
    }

    // What the resumed run answers it saw.
    private static async Task<string?> Saw(global::app.data.@this result, string name)
    {
        await result.IsSuccess();
        return (await (await result.Get(name)).Value())?.ToString();
    }

    // `save snapshot`, run where G's second step stands, after a note was set.
    private static async Task<global::app.data.@this> Saved(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        var step = await Holds(app);
        await context.Variable.Set("note", "original");
        await using var frame = context.call.Push(step, await context.call.Setting());
        await using var call = context.call.Push(step.Code[0], await context.call.Setting(), context.Variable);
        var saved = await new global::app.module.snapshot.save(context).Start();
        await call.DisposeAsync();
        await frame.DisposeAsync();
        return saved;
    }

    private static async Task<byte[]> Plang(global::app.data.@this data, global::app.actor.context.@this context)
    {
        using var file = new MemoryStream();
        await (await context.App.type.list.Format("plang")!.Encode(file, data, context)).IsSuccess();
        return file.ToArray();
    }

    private static Task<global::app.data.@this> Read(byte[] file, global::app.actor.context.@this context)
        => context.App.type.list.Format("plang")!.Decode(file, context, "snap");

    private static async Task<global::app.data.@this> Resume(global::app.data.@this snapshot, global::app.actor.context.@this context)
    {
        await context.Variable.Set("snap", snapshot);
        return await (await context.Action("snapshot.resume(Snapshot=%snap%)")).Start(context);
    }

    [Test]
    public async Task AFreshApp_GoesOn_FromTheFirstAppsSignedPlang()
    {
        await using var first = App();
        var file = await Plang(await Saved(first), first.actor.list.User.Context);

        await using var fresh = App();
        await Holds(fresh);
        var context = fresh.actor.list.User.Context;
        var read = await Read(file, context);
        await read.IsSuccess();

        var result = await Resume(read, context);
        await Assert.That(await Saw(result, "s1")).IsEqualTo("second");
        await Assert.That(await Saw(result, "note")).IsEqualTo("original");
    }

    // The road a program takes: `save %run% to file 'run.plang'`, then `read file 'run.plang', write to %snap%`. The
    // read holds the file; when it opens, the variable takes what was read whole, its signature with it.
    [Test]
    public async Task APlangReadThroughAFile_GoesOn()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await context.Variable.Set("run", await Saved(app));
        await (await (await context.Action("file.save(Path=\"run.plang\", Value=%run%)")).Start(context)).IsSuccess();
        await context.Variable.Set("s1", "not yet");

        var read = await (await context.Action("file.read(Path=\"run.plang\")")).Start(context);
        await read.IsSuccess();

        var result = await Resume(read, context);
        await Assert.That(await Saw(result, "s1")).IsEqualTo("second");
        // the run went on in its own app: the asker's %s1% is as it set it
        await Assert.That(await context.Variable.GetValue("s1")).IsEqualTo("not yet");
    }

    // What a .plang holds beside the value comes back through a file read too: its properties (`!`) and what a program
    // added to the value (`.`).
    [Test]
    public async Task APlangReadThroughAFile_KeepsItsPropertiesAndAddedProperties()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var value = new global::app.data.@this("x", "box", context: context);
        value.Properties["colour"] = "red";
        value.Property.Add("lang", new global::app.data.@this("lang", "is", context: context));
        await context.Variable.Set("x", value);
        await (await (await context.Action("file.save(Path=\"x.plang\", Value=%x%)")).Start(context)).IsSuccess();

        await context.Variable.Set("y", await (await context.Action("file.read(Path=\"x.plang\")")).Start(context));
        await Assert.That(await context.Variable.GetValue("y")).IsEqualTo("box");

        await Assert.That(await context.Variable.GetValue("y!colour")).IsEqualTo("red");
        await Assert.That(await context.Variable.GetValue("y.lang")).IsEqualTo("is");
    }

    // A file kept at rest is read as often as wanted: its nonce is no replay, and no window ends it.
    [Test]
    public async Task OnePlang_GoesOnTwice_PastTheWindow()
    {
        await using var first = App();
        var file = await Plang(await Saved(first), first.actor.list.User.Context);

        await using var fresh = App();
        await Holds(fresh);
        var context = fresh.actor.list.User.Context;
        await Short(context);
        await Task.Delay(50);

        await (await Resume(await Read(file, context), context)).IsSuccess();
        await (await Resume(await Read(file, context), context)).IsSuccess();
    }

    [Test]
    public async Task AStoreRow_Verifies_AsAFileDoes()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        await (await app.store.Set("snapshots", "run", await Saved(app))).IsSuccess();

        var row = await app.store.Get<global::app.snapshot.@this>("snapshots", "run");
        await row.IsSuccess();
        await Assert.That(row.Signature).IsNotNull();
        await (await Resume(row, context)).IsSuccess();
    }

    [Test]
    public async Task AHandEdit_IsRefused()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var file = System.Text.Encoding.UTF8.GetString(await Plang(await Saved(app), context));
        await Assert.That(file).Contains("original");

        var read = await Read(System.Text.Encoding.UTF8.GetBytes(file.Replace("original", "changed!")), context);

        await read.IsFailure();
        await Assert.That(read.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test]
    public async Task AnotherAppsSnapshot_IsRefused()
    {
        await using var other = App(new Keys("other"));
        var file = await Plang(await Saved(other), other.actor.list.User.Context);

        await using var app = App();
        await Holds(app);
        var context = app.actor.list.User.Context;
        var result = await Resume(await Read(file, context), context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SnapshotNotTheApps");
    }

    [Test]
    public async Task ASnapshotSignedByAnArchivedIdentity_IsRefused()
    {
        var keys = new Keys("signer");
        await using var signer = App(keys);
        var file = await Plang(await Saved(signer), signer.actor.list.User.Context);

        await using var app = App(keys.Archived());
        await Holds(app);
        var context = app.actor.list.User.Context;
        var result = await Resume(await Read(file, context), context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SnapshotNotTheApps");
    }

    [Test]
    public async Task AnUnsignedSnapshot_IsRefused()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;

        // every snapshot the app takes is signed: one that isn't was made some other way
        var result = await Resume(new global::app.data.@this("snap", new global::app.snapshot.@this(context), context: context), context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SnapshotUnsigned");
    }

    // Every snapshot the app takes is born signed by its own identity — an ask's pending state and an error's callback
    // too, before anything carries it.
    [Test]
    public async Task ASnapshotTheAppTakes_IsSigned()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;

        var taken = await app.Snapshot(context);

        await taken.IsSuccess();
        await Assert.That(taken.Signature).IsNotNull();
    }

    // A Data signed for plang's own store, read from a .plang file, is never judged at rest: its type states no view of
    // its own, so it is re-hashed in Out, and its Out bytes are not the Store bytes it was signed over: refused.
    [Test]
    public async Task AStoreSignedData_FromAFile_IsNeverAtRest()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var file = await Plang(await Signed(context, store: true), context);

        var read = await Read(file, context);

        await read.IsFailure();
        await Assert.That(read.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test]
    public async Task ASensitiveLeaf_ComesBackNull()
    {
        await using var src = App();
        var context = src.actor.list.User.Context;
        var secret = new global::app.type.item.sensitive.@this("the-secret", src.type.list[typeof(global::app.type.item.sensitive.@this)]);
        await context.Variable.Set("login", new global::app.data.@this("login",
            new Dictionary<string, object?> { ["user"] = "ingi", ["password"] = secret }, context: context));
        var text = await src.Snapshot(context).Saved().Text(context);
        await Assert.That(text).DoesNotContain("the-secret");

        await using var dst = App();
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        await Assert.That(await into.Variable.GetValue("login.user")).IsEqualTo("ingi");
        await Assert.That(await into.Variable.GetValue("login.password")).IsNull();
    }

    // A Data signed for the store (store: true) or for the wire, by the app's identity.
    // Signed through the signer itself, as C# that knows its view signs: the view handed whole.
    private static async Task<global::app.data.@this> Signed(global::app.actor.context.@this context, bool store)
        => await context.App.Code.Get<global::app.module.signing.code.ISigning>().Provider!.SignAsync(new global::app.module.signing.sign(context)
        {
            Data = context.Ok("hello"),
            View = store ? global::app.View.Store : global::app.View.Out,
        });

    // The signing setting's window made a millisecond, so a signature read live is past it at once.
    private static async Task Short(global::app.actor.context.@this context)
    {
        var store = context.App.actor.list.System.Setting;
        store.Set(store.Address(typeof(global::app.module.signing.setting.@this), "expiry"), global::System.TimeSpan.FromMilliseconds(1));
        await Task.CompletedTask;
    }

    // An identity provider holding one keypair of its own, its default and only listed identity. Archived, the keypair
    // is held no more as the app's: the app signs as a new default, and the provider lists only that one (archived
    // identities are never listed, nor ever the default).
    private sealed class Keys : global::app.module.identity.code.IIdentity
    {
        private readonly (string PublicKey, string PrivateKey) _keys;
        public Keys(string name) { Name = name; _keys = Make(); }
        public Keys Archived() => new(Name + "-archived");

        private static (string, string) Make()
        {
            var (pair, _) = new global::app.module.signing.code.Ed25519().GenerateKeyPair();
            return (pair!.PublicKey, pair.PrivateKey);
        }

        public string Name { get; }
        public bool IsBuiltIn { get; set; } = true;
        public string? Source { get; set; }

        private Identity Mine(global::app.module.IContext a) => new("default", a.Context.App.type.list[typeof(Identity)])
            { PublicKey = _keys.PublicKey, PrivateKey = _keys.PrivateKey, IsDefault = true };
        private global::app.data.@this<Identity> Ok(global::app.module.IContext a) => a.Context.Ok<Identity>(Mine(a));

        public Task<global::app.data.@this<Identity>> GetAsync(global::app.module.identity.Get action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> GetOrCreateDefaultAsync(global::app.module.IContext action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> CreateAsync(global::app.module.identity.Create action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> ArchiveAsync(global::app.module.identity.Archive action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> UnarchiveAsync(global::app.module.identity.Unarchive action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> SetDefaultAsync(global::app.module.identity.SetDefault action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> RenameAsync(global::app.module.identity.Rename action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> ExportAsync(global::app.module.identity.Export action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<global::app.type.item.list.@this<Identity>>> ListAsync(global::app.module.identity.list action)
            => Task.FromResult(action.Context.Ok<global::app.type.item.list.@this<Identity>>(new global::app.type.item.list.@this<Identity>(
                new global::app.data.@this[] { action.Context.Ok<Identity>(Mine(action)) },
                action.Context.App.type.list[typeof(global::app.type.item.list.@this<Identity>)])));
    }
}
