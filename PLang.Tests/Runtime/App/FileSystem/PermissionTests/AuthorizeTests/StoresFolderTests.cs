using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.AuthorizeTests;

/// The stores' folder (<c>&lt;root&gt;/.data/</c>) holds the settings' rows, an identity's private key among them: it is
/// not in-root. The system reaches it through its standing grant; any other actor is refused, never asked — a
/// channel that would answer "a" is never asked at all.
public class StoresFolderTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string key;
    private readonly global::app.@this app;
    private readonly Asking channel;

    public StoresFolderTests()
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-data-" + System.Guid.NewGuid().ToString("N")[..8]);
        key = System.IO.Path.Combine(root, ".data", "setting", "data.sqlite");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(key)!);
        System.IO.File.WriteAllText(key, "the identity's private key");
        app = new global::app.@this(root).TestSigning();
        channel = new Asking(app.actor.list.User.Channel, app.actor.list.User.Context);
        app.actor.list.User.Channel.Register(channel);
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // An input that grants whatever it is asked, counting the asks.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public Asking(global::app.channel.list.@this channels, global::app.actor.context.@this ctx) : base(channels)
        {
            _ctx = ctx; Name = "input";
            Direction = global::app.channel.ChannelDirection.Bidirectional;
        }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok("a"));
        }
    }

    // the file's text, read beside whoever holds it open (the app's settings store does; Windows refuses a plain open)
    private static string Shared(string path)
    {
        using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read,
            System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task Refused(global::app.data.@this answer)
    {
        await answer.IsFailure();
        await Assert.That(answer.Error!.Key).IsEqualTo("PermissionDenied");
        await Assert.That(channel.Count).IsEqualTo(0);
    }

    [Test] public async Task AUserReadingTheSettingsStore_IsRefused_NeverAsked()
    {
        var user = app.actor.list.User.Context;
        await Refused(await user.File(key).Read(user));
    }

    [Test] public async Task AUserCopyingTheSettingsStoreOut_IsRefused_NeverAsked()
    {
        var user = app.actor.list.User.Context;
        await Refused(await user.File(key).CopyTo(user.File(System.IO.Path.Combine(root, "stolen")), overwrite: true, subfolder: true, user));
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(root, "stolen"))).IsFalse();
    }

    [Test] public async Task AUserCopyingIntoTheStoresFolder_IsRefused_NeverAsked()
    {
        var user = app.actor.list.User.Context;
        var mine = System.IO.Path.Combine(root, "mine");
        System.IO.File.WriteAllText(mine, "rows");
        await Refused(await user.File(mine).CopyTo(user.File(key), overwrite: true, subfolder: true, user));
        await Assert.That(Shared(key)).IsEqualTo("the identity's private key");
    }

    // the OS-rooted form of the same file (//<root>/…, a .. in it too) is the same file to the gate
    [Test]
    [Arguments("")]
    [Arguments("/sub/..")]
    public async Task TheDoubleSlashForm_IsRefused_NeverAsked(string detour)
    {
        if (System.OperatingSystem.IsWindows()) return;   // // is the OS-rooted form on Unix only
        var user = app.actor.list.User.Context;
        var form = "/" + root + detour + "/.data/setting/data.sqlite";
        await Refused(await user.File(form).Read(user));
    }

    // the stores' folder in any case is refused: a case-blind disk (macOS) opens .DATA as .data
    [Test] public async Task TheStoresFolder_InAnyCase_IsRefused_NeverAsked()
    {
        var shout = System.IO.Path.Combine(root, ".DATA", "setting", "data.sqlite");
        if (!System.IO.File.Exists(shout))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(shout)!);
            System.IO.File.WriteAllText(shout, "the identity's private key");
        }
        var user = app.actor.list.User.Context;
        await Refused(await user.File(shout).Read(user));
    }

    // Windows: the stores' folder by its 8.3 short name (DATA~1) is the stores' folder — refused, never asked
    [Test] public async Task TheStoresFolder_ByItsShortName_IsRefused_NeverAsked()
    {
        if (!System.OperatingSystem.IsWindows()) return;
        var folder = System.IO.Path.Combine(root, ".data");
        var buffer = new char[32768];
        var length = GetShortPathNameW(folder, buffer, (uint)buffer.Length);
        var shortened = length == 0 ? folder : new string(buffer, 0, (int)length);
        if (string.Equals(System.IO.Path.GetFileName(shortened), ".data", System.StringComparison.OrdinalIgnoreCase))
        {
            // the volume makes no short names: nothing to test here
            System.Console.WriteLine("8.3 short names are off on this volume — not applicable");
            return;
        }
        var user = app.actor.list.User.Context;
        await Refused(await user.File(System.IO.Path.Combine(shortened, "setting", "data.sqlite")).Read(user));
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string longPath, char[] shortPath, uint size);

    [Test] public async Task TheSystem_ReadsTheStoresFolder_ThroughItsStandingGrant()
    {
        var system = app.actor.list.System.Context;
        await (await system.File(key).Read(system)).IsSuccess();
    }
}

/// The stores open in the stores' folder, on disk, with the system's context: the app's and the settings'. A first run
/// has no stores' folder: the app's store, opened before any setting is read, makes it itself.
public class StoresFolderOpenTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-stores-" + System.Guid.NewGuid().ToString("N")[..8])).TestSigning();

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task TheAppsStoreAndTheSettingsStore_StillOpen()
    {
        var system = app.actor.list.System.Context;
        await Assert.That(System.IO.Directory.Exists(System.IO.Path.Combine(app.filesystem.root.Absolute, ".data"))).IsFalse();
        // the app's store first, by a read that signs nothing (a write signs, which reads a setting, which opens the
        // settings' store — whose folder under the stores' folder would make it first)
        await (await app.store.Tables()).IsSuccess();
        await (await app.store.Set("probe", "k", new global::app.data.@this("k", "v", context: system))).IsSuccess();
        await (await app.actor.list.System.Setting.Load()).IsSuccess();
        await (await app.actor.list.User.Setting.Load()).IsSuccess();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(app.filesystem.root.Absolute, ".data", "data.sqlite"))).IsTrue();
        await Assert.That(System.IO.File.Exists(System.IO.Path.Combine(app.filesystem.root.Absolute, ".data", "setting", "data.sqlite"))).IsTrue();
    }

    // the system's standing grant covers the stores' folder itself, not only what is under it: the app's store makes it
    // (its parent) on a first run, and a refusal there was hidden only because reading a grant opens the settings' store,
    // whose own folder under it makes it first
    [Test] public async Task TheSystem_MakesTheStoresFolder_OnAFreshRoot()
    {
        var system = app.actor.list.System.Context;
        var folder = System.IO.Path.Combine(app.filesystem.root.Absolute, ".data");
        await Assert.That(System.IO.Directory.Exists(folder)).IsFalse();
        await (await system.File(folder).Mkdir(system)).IsSuccess();
        await (await system.File(folder).Authorize(global::app.type.item.permission.Verb.read, system)).IsSuccess();
    }

    // the os folder's standing grant covers the folder itself too: every actor reads it
    [Test] public async Task EveryActor_ReadsTheOsFolderItself()
    {
        var user = app.actor.list.User.Context;
        await (await user.File(app.filesystem.os.Absolute).Authorize(global::app.type.item.permission.Verb.read, user)).IsSuccess();
    }
}
