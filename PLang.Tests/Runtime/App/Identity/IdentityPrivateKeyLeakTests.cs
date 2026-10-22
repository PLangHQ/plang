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

    // ---- what the action hands the step ----

    [Test]
    public async Task Export_DoesNotHandTheStepThePrivateKey()
    {
        var got = await Run("identity.export()");
        await Assert.That(got.Success).IsTrue();
        await Assert.That(((IdentityRecord)(await got.Value())!).PrivateKey).IsNotEqualTo(Key);
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
        var got = await Run("identity.export()");
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
        var got = await Run("identity.export()");
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
