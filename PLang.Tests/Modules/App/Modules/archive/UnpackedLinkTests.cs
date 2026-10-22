using System.Formats.Tar;

namespace PLang.Tests.App.Modules.archive;

/// <summary>
/// A link an unpack leaves on disk is read by the OS for every step after it: one whose target leaves the folder it was
/// unpacked into (absolute from the host's root, or <c>..</c> past it) would let a later read or write under the folder
/// reach outside it with no ask.
/// </summary>
public class UnpackedLinkTests : IDisposable
{
    private readonly string _root;
    private readonly string _outside;
    private readonly global::app.@this _app;
    private readonly Asking _channel;

    public UnpackedLinkTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_link_" + Guid.NewGuid().ToString("N"))).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_link_out_" + Guid.NewGuid().ToString("N"))).FullName;
        System.IO.File.WriteAllText(Path.Combine(_outside, "secret.txt"), "outside");
        _app = new global::app.@this(_root).Testing();
        _channel = new Asking(Context);
        _app.actor.list.User.Channel.Register(_channel);
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
        Directory.Delete(_outside, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    // An input that refuses whatever it is asked, counting the asks.
    private sealed class Asking : global::app.channel.@this
    {
        private readonly global::app.actor.context.@this _ctx;
        public int Count { get; private set; }
        public Asking(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            Count++;
            return Task.FromResult(action.Context.Ok("n"));
        }
    }

    private static MemoryStream Tar(params TarEntry[] entries)
    {
        var tar = new MemoryStream();
        using (var writer = new TarWriter(tar, TarEntryFormat.Pax, leaveOpen: true))
            foreach (var entry in entries) writer.WriteEntry(entry);
        tar.Position = 0;
        return tar;
    }

    private static PaxTarEntry Link(string name, string target) => new(TarEntryType.SymbolicLink, name) { LinkName = target };

    private async Task<global::app.data.@this> Into(Stream archive, string folder)
    {
        var tar = (global::app.module.archive.type.archive.kind.tar.@this)Context.App.type.list[typeof(global::app.module.archive.type.archive.@this)].kind["tar"]!;
        return await tar.Unpack(archive, null, Context.File(Path.Combine(_root, folder)), global::app.type.item.size.@this.Create(100 * 1024 * 1024, Context), Context);
    }

    // after the unpack, a read under the folder through the link: it must never read outside unasked
    private async Task NothingOutsideIsReadThrough(string target)
    {
        await Into(Tar(Link("lnk", target)), "up");
        var read = await Context.File(Path.Combine(_root, "up", "lnk", "secret.txt")).Read(Context);
        var leaked = read.Success && (await read.Value())?.ToString() == "outside";
        await Assert.That(leaked).IsFalse();
    }

    [Test] public async Task AnAbsoluteLinkOutOfTheFolder_ReadsNothingOutside()
    {
        if (!OperatingSystem.IsLinux()) return;
        await NothingOutsideIsReadThrough(_outside);
    }

    // a link that climbs with .. but stays inside (npm's node_modules/.bin/x → ../pkg/bin/x) unpacks as it is written
    [Test] public async Task AnInsideLinkWithDotDot_UnpacksAsWritten_AndReads()
    {
        if (!OperatingSystem.IsLinux()) return;
        var file = new PaxTarEntry(TarEntryType.RegularFile, "node_modules/pkg/bin/x.txt") { DataStream = new MemoryStream("run"u8.ToArray()) };
        await (await Into(Tar(file, Link("node_modules/.bin/x.txt", "../pkg/bin/x.txt")), "up")).IsSuccess();
        await Assert.That(new FileInfo(Path.Combine(_root, "up/node_modules/.bin/x.txt")).LinkTarget).IsEqualTo("../pkg/bin/x.txt");
        var read = await Context.File(Path.Combine(_root, "up", "node_modules", ".bin", "x.txt")).Read(Context);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("run");
        await Assert.That(_channel.Count).IsEqualTo(0);
    }

    [Test] public async Task ARelativeLinkClimbingOut_ReadsNothingOutside()
    {
        if (!OperatingSystem.IsLinux()) return;
        await NothingOutsideIsReadThrough(string.Join('/', Enumerable.Repeat("..", 20)) + _outside);
    }
}
