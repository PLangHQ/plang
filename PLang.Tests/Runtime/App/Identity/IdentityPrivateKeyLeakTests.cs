using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using IdentityRecord = global::app.module.identity.Identity;

namespace PLang.Tests.Runtime.App.IdentityLeak;

/// Security (item-doors, K): a user step can get an identity through the identity actions (export, get, list). The
/// private key is [Sensitive, Store]: hidden in the Out and Debug views, kept in the Store view. A step that
/// writes what it got through a Store-view sink (a .pr file, a snapshot) must not carry the key out. Each test
/// asserts the SAFE behaviour: red where the key crosses.
public class IdentityPrivateKeyLeakTests : System.IAsyncDisposable
{
    // TestSigning's key mock hands every generated pair this private key
    private const string Key = "test-private-key";

    private readonly string root;
    private readonly global::app.@this app;

    public IdentityPrivateKeyLeakTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-sec-k-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        app = new global::app.@this(root).TestSigning();
    }
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this User => app.actor.list.User.Context;

    // A user step's own action, as a step runs it
    private async Task<global::app.data.@this> Run(string formal)
    {
        var action = await User.Action(formal);
        return await action.Start(User);
    }

    // identity.export on an app whose default identity is made first (identity.get asks nothing): an app gives out
    // only an identity it holds
    private async Task<global::app.data.@this> Export()
    {
        await Run("identity.get()");
        return await Run("identity.export()");
    }

    // ---- what the action hands the step ----

    // export waits for critical actions: refused, whatever the actor would answer
    [Test]
    public async Task Export_WithoutTheActorsYes_IsRefused()
    {
        var got = await Export();
        await Assert.That(got.Success).IsFalse();
        await Assert.That(got.Error!.Key).IsEqualTo("ExportWaitsForCriticalActions");
    }

    // an actor that would say "always" is never handed the key
    [Test]
    public async Task Export_Refuses_UntilCriticalActions()
    {
        app.actor.list.User.Channel.Register(new Asking(User, "a"));
        var got = await Export();
        await Assert.That(got.Error?.Key).IsEqualTo("ExportWaitsForCriticalActions");
        await Assert.That(got.Success ? ((IdentityRecord?)await got.Value())?.PrivateKey : null).IsNotEqualTo(Key);
    }

    // the refusal is no "no": the actor is never asked
    [Test]
    public async Task TheExportQuestion_IsNeverAsked()
    {
        var asking = new Asking(User, "a");
        app.actor.list.User.Channel.Register(asking);
        await Export();
        await Assert.That(asking.Count).IsEqualTo(0);
    }

    // a change to the app's identities asks the actor: without its yes it is refused, nothing made
    [Test]
    public async Task Create_WithoutTheActorsYes_IsRefused()
    {
        var made = await Run("identity.create(Name=\"mine\")");
        await Assert.That(made.Success).IsFalse();
        await Assert.That(made.Error!.Key).IsEqualTo("PermissionDenied");
    }

    // %MyIdentity% is the app's identity as a step may hold it: no private key
    [Test]
    public async Task MyIdentity_HoldsNoPrivateKey()
    {
        var mine = await User.Variable.Get("MyIdentity");
        await Assert.That(((IdentityRecord)(await mine.Value())!).PrivateKey).IsNotEqualTo(Key);
    }

    // An input that answers whatever it is asked with one answer, counting the asks.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        private readonly string _answer;
        public int Count { get; private set; }
        public Asking(global::app.actor.context.@this ctx, string answer) { _ctx = ctx; _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok(_answer));
        }
    }

    [Test]
    public async Task Get_DoesNotHandTheStepThePrivateKey()
    {
        var got = await Run("identity.get()");
        await Assert.That(got.Success).IsTrue();
        await Assert.That(((IdentityRecord)(await got.Value())!).PrivateKey).IsNotEqualTo(Key);
    }

    [Test]
    public async Task List_DoesNotHandTheStepAnyPrivateKey()
    {
        await Run("identity.get()");      // the default is made
        var got = await Run("identity.list()");
        await Assert.That(got.Success).IsTrue();
        var keys = new List<string>();
        foreach (var each in ((global::app.type.item.list.@this<IdentityRecord>)(await got.Value())!).Items(User))
            keys.Add(((IdentityRecord)(await each.Value())!).PrivateKey);
        await Assert.That(keys.Any(k => k == Key)).IsFalse();
    }

    // ---- Store-view sinks a step can reach with what it got ----

    [Test]
    public async Task ExportedIdentity_SavedToAPrFile_DoesNotCarryThePrivateKey()
    {
        var got = await Export();
        await User.Variable.Set("id", await got.Value());
        var saved = await (await User.Action("file.save(Path=\"/out.pr\", Value=%id%)")).Start(User);
        await Assert.That(saved.Success).IsTrue();
        await Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "out.pr")).Contains(Key)).IsFalse();
    }

    [Test]
    public async Task GotIdentity_SavedToAPrFile_DoesNotCarryThePrivateKey()
    {
        var got = await Run("identity.get()");
        await User.Variable.Set("id", await got.Value());
        var saved = await (await User.Action("file.save(Path=\"/out.pr\", Value=%id%)")).Start(User);
        await Assert.That(saved.Success).IsTrue();
        await Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(root, "out.pr")).Contains(Key)).IsFalse();
    }

    [Test]
    public async Task ExportedIdentity_InASnapshotsWire_DoesNotCarryThePrivateKey()
    {
        var got = await Export();
        await User.Variable.Set("id", await got.Value());
        var wire = await app.SnapshotToWire(app.Snapshot(User));
        await Assert.That(wire.Contains(Key)).IsFalse();
    }

    [Test]
    public async Task GotIdentity_InASnapshotsWire_DoesNotCarryThePrivateKey()
    {
        var got = await Run("identity.get()");
        await User.Variable.Set("id", await got.Value());
        var wire = await app.SnapshotToWire(app.Snapshot(User));
        await Assert.That(wire.Contains(Key)).IsFalse();
    }
}
