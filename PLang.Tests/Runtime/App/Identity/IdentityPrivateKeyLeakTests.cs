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

    // export is the one door to the key: refused without the actor's yes (a closed input answers no)
    [Test]
    public async Task Export_WithoutTheActorsYes_IsRefused()
    {
        var got = await Export();
        await Assert.That(got.Success).IsFalse();
        await Assert.That(got.Error!.Key).IsEqualTo("PermissionDenied");
    }

    // with a yes it hands the key, and asks again the next time ("always" keeps nothing)
    [Test]
    public async Task Export_WithTheActorsYes_HandsTheKey_AskedEveryTime()
    {
        var asking = new Asking(User, "a");
        app.actor.list.User.Channel.Register(asking);
        var got = await Export();
        await Assert.That(((IdentityRecord)(await got.Value())!).PrivateKey).IsEqualTo(Key);
        await Run("identity.export()");
        await Assert.That(asking.Count).IsEqualTo(2);
    }

    // the export question says what a yes means: the key as text the step can send anywhere, this call only, asked
    // every time, never kept
    [Test]
    public async Task TheExportQuestion_SaysWhatAYesMeans()
    {
        var asking = new Asking(User, "n");
        app.actor.list.User.Channel.Register(asking);
        await Export();
        await Assert.That(asking.Question).Contains("send anywhere");
        await Assert.That(asking.Question).Contains("this call only");
        await Assert.That(asking.Question).Contains("every time");
        await Assert.That(asking.Question).Contains("never kept");
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
        public string Question { get; private set; } = "";
        public Asking(global::app.actor.context.@this ctx, string answer) { _ctx = ctx; _answer = answer; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            Question = action.Question.Peek()?.ToString() ?? "";
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
