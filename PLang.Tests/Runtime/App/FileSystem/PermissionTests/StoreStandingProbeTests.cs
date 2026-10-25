namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Security probes for ac584f4e1 (the stores' folder opens only to what the actor's role stands on): who reads a key in
/// the stores' folder. Each asserts the SAFE behaviour: red is a hole.
public class StoreStandingProbeTests
{
    private static string NewRoot(string under)
    {
        var root = System.IO.Path.Combine(under, "plang-sp-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, ".data"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, ".data", "secret.key"), "SECRET-KEY");
        return root;
    }

    private sealed class Asking : global::app.channel.@this
    {
        public int Asked;
        private readonly global::app.actor.context.@this _ctx;
        public Asking(global::app.channel.list.@this channels, global::app.actor.context.@this ctx) : base(channels) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        { Asked++; return Task.FromResult(_ctx.Ok("y")); }
    }

    [Test]
    public async Task TheUser_CannotReadTheStoresKey_AndIsNotAsked()
    {
        var root = NewRoot(System.IO.Path.GetTempPath());
        await using var app = new global::app.@this(root).TestSigning();
        var asking = new Asking(app.actor.list.User.Channel, app.actor.list.User.Context);
        app.actor.list.User.Channel.Register(asking);
        var user = app.actor.list.User.Context;
        var r = await user.File(System.IO.Path.Combine(root, ".data", "secret.key")).Read(user);
        await Assert.That(r.Success).IsFalse();
        await Assert.That(asking.Asked).IsEqualTo(0);
    }

    [Test]
    public async Task TheSystem_ReadsAndWritesItsOwnStoresFile()
    {
        var root = NewRoot(System.IO.Path.GetTempPath());
        await using var app = new global::app.@this(root).TestSigning();
        var system = app.actor.list.System.Context;
        var file = System.IO.Path.Combine(root, ".data", "secret.key");
        await Assert.That((await system.File(file).Read(system)).Success).IsTrue();
        await Assert.That((await system.File(file).WriteText("rewritten", system)).Success).IsTrue();
    }

    // an app whose root lies under the runtime's os folder: its stores' folder is then under the os folder, where every
    // user stands read — the user must still not read the key
    [Test]
    public async Task AnAppRootedUnderTheOsFolder_TheUserStillCannotReadTheStoresKey()
    {
        var os = System.IO.Path.Combine(System.AppContext.BaseDirectory, "os");
        System.IO.Directory.CreateDirectory(os);
        var root = NewRoot(os);
        try
        {
            await using var app = new global::app.@this(root).TestSigning();
            var user = app.actor.list.User.Context;
            var r = await user.File(System.IO.Path.Combine(root, ".data", "secret.key")).Read(user);
            await Assert.That(r.Success).IsFalse();
        }
        finally { System.IO.Directory.Delete(root, true); }
    }
}
