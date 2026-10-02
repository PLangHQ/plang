using System.Formats.Tar;
using File = global::app.type.item.path.file.@this;

namespace PLang.Tests.App.Modules.archive;

/// <summary>
/// A bundle unpacked into a folder, and nothing of it outside the folder — not by <c>..</c>, not through a link in the
/// folder (absolute or relative), not by a hard link to a file outside. As a layer, its whiteouts remove. The guards are
/// the bundle base's, so every format has them.
/// </summary>
public class UnpackTests : IDisposable
{
    private readonly string _root;
    private readonly string _outside;
    private readonly global::app.@this _app;

    public UnpackTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_unpack_" + Guid.NewGuid().ToString("N"))).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_unpack_out_" + Guid.NewGuid().ToString("N"))).FullName;
        _app = new global::app.@this(_root).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
        Directory.Delete(_outside, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    /// <summary>A tar holding <paramref name="entries"/>.</summary>
    private static MemoryStream Tar(params TarEntry[] entries)
    {
        var tar = new MemoryStream();
        using (var writer = new TarWriter(tar, TarEntryFormat.Pax, leaveOpen: true))
            foreach (var entry in entries) writer.WriteEntry(entry);
        tar.Position = 0;
        return tar;
    }

    private static PaxTarEntry File_(string name, string content) =>
        new(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)) };

    private static PaxTarEntry Link(string name, string target) => new(TarEntryType.SymbolicLink, name) { LinkName = target };

    private async Task<global::app.data.@this> Into(Stream archive, string folder, bool layer = false, long max = 100 * 1024 * 1024)
    {
        global::app.module.archive.type.archive.kind.bundle.@this bundle = layer
            ? new global::app.module.archive.type.archive.kind.oci.layer.@this()
            : new global::app.module.archive.type.archive.kind.tar.@this();
        return await bundle.Unpack(archive, null, new File(Path.Combine(_root, folder)), new global::app.type.item.size.@this(max, Context), Context);
    }

    [Test]
    public async Task Files_Folders_AndLinks_AreUnpacked()
    {
        if (!OperatingSystem.IsLinux()) return;
        var archive = Tar(new PaxTarEntry(TarEntryType.Directory, "etc/"), File_("etc/os", "alpine"), Link("bin/sh", "/bin/busybox"),
            File_("bin/busybox", "#!"));
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "root/etc/os"))).IsEqualTo("alpine");
        await Assert.That(new FileInfo(Path.Combine(_root, "root/bin/sh")).LinkTarget).IsEqualTo("/bin/busybox");
    }

    [Test]
    public async Task DotDot_StaysInside()
    {
        if (!OperatingSystem.IsLinux()) return;
        var escape = Path.GetRelativePath(Path.Combine(_root, "root"), Path.Combine(_outside, "dotdot.txt"));
        await (await Into(Tar(File_(escape, "x")), "root")).IsSuccess();
        await Assert.That(System.IO.File.Exists(Path.Combine(_outside, "dotdot.txt"))).IsFalse();
    }

    [Test]
    public async Task AnAbsoluteSymlink_IsTheFoldersOwn_NeverTheHosts()
    {
        if (!OperatingSystem.IsLinux()) return;
        // lib -> <outside> (absolute), then lib/evil.txt: written under the folder's own <outside>, never the host's
        var archive = Tar(Link("lib", _outside), File_("lib/evil.txt", "x"));
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.Exists(Path.Combine(_outside, "evil.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root" + _outside, "evil.txt"))).IsTrue();
    }

    [Test]
    public async Task ARelativeSymlinkClimbingOut_StaysInside()
    {
        if (!OperatingSystem.IsLinux()) return;
        var up = string.Join('/', Enumerable.Repeat("..", 20));
        var archive = Tar(Link("up", up + _outside), File_("up/evil.txt", "x"));
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.Exists(Path.Combine(_outside, "evil.txt"))).IsFalse();
    }

    [Test]
    public async Task AFileOverASymlink_ReplacesTheLink_NotItsTarget()
    {
        if (!OperatingSystem.IsLinux()) return;
        System.IO.File.WriteAllText(Path.Combine(_outside, "kept.txt"), "kept");
        var archive = Tar(Link("x", Path.Combine(_outside, "kept.txt")), File_("x", "replaced"));
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_outside, "kept.txt"))).IsEqualTo("kept");
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "root/x"))).IsEqualTo("replaced");
    }

    [Test]
    public async Task AHardLinkToAFileOutside_CopiesNothingFromOutside()
    {
        if (!OperatingSystem.IsLinux()) return;
        System.IO.File.WriteAllText(Path.Combine(_outside, "secret.txt"), "secret");
        var archive = Tar(new PaxTarEntry(TarEntryType.HardLink, "stolen") { LinkName = _outside.TrimStart('/') + "/../" + Path.GetFileName(_outside) + "/secret.txt" });
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root/stolen"))).IsFalse();
    }

    [Test]
    public async Task AHardLinkToAFileInside_IsACopyOfIt()
    {
        if (!OperatingSystem.IsLinux()) return;
        var archive = Tar(File_("a.txt", "same"), new PaxTarEntry(TarEntryType.HardLink, "b.txt") { LinkName = "a.txt" });
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "root/b.txt"))).IsEqualTo("same");
    }

    [Test]
    public async Task AsALayer_WhiteoutsRemove_AndAnOpaqueFolderEmpties()
    {
        if (!OperatingSystem.IsLinux()) return;
        await (await Into(Tar(File_("a.txt", "a"), File_("d/old.txt", "o"), File_("keep.txt", "k")), "root", layer: true)).IsSuccess();
        await (await Into(Tar(File_(".wh.a.txt", ""), File_("d/.wh..wh..opq", ""), File_("d/new.txt", "n")), "root", layer: true)).IsSuccess();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root/a.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root/d/old.txt"))).IsFalse();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root/d/new.txt"))).IsTrue();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root/keep.txt"))).IsTrue();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root/.wh.a.txt"))).IsFalse();
    }

    [Test]
    public async Task AGzippedLayer_IsRead_AsItsTar()
    {
        if (!OperatingSystem.IsLinux()) return;
        var gz = new MemoryStream();
        using (var zip = new System.IO.Compression.GZipStream(gz, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
            Tar(File_("in/layer.txt", "zipped")).CopyTo(zip);
        gz.Position = 0;
        await (await Into(gz, "root", layer: true)).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "root/in/layer.txt"))).IsEqualTo("zipped");
    }

    [Test]
    public async Task OnlyLeadingDotSlash_IsStripped_NotAHiddenNamesDot()
    {
        if (!OperatingSystem.IsLinux()) return;
        await (await Into(Tar(File_("./.bashrc", "rc"), File_("/etc/x", "x")), "root")).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "root/.bashrc"))).IsEqualTo("rc");
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "root/etc/x"))).IsEqualTo("x");
    }

    [Test]
    public async Task ADevice_IsNeverMade_AndSetuidIsDropped()
    {
        if (!OperatingSystem.IsLinux()) return;
        var setuid = File_("bin/su", "#!");
        setuid.Mode = UnixFileMode.SetUser | UnixFileMode.UserExecute;
        var archive = Tar(new PaxTarEntry(TarEntryType.CharacterDevice, "dev/null"), setuid);
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.Exists(Path.Combine(_root, "root/dev/null"))).IsFalse();
        var mode = System.IO.File.GetUnixFileMode(Path.Combine(_root, "root/bin/su"));
        await Assert.That(mode.HasFlag(UnixFileMode.SetUser)).IsFalse();
        await Assert.That(mode.HasFlag(UnixFileMode.UserRead | UnixFileMode.UserWrite)).IsTrue();
    }

    // An entry named the folder itself — `..` or `a/..` — never lands: nothing is written over the folder, and nothing
    // over the folder above it.
    [Test]
    public async Task AnEntryNamedTheFolderItself_NeverLands()
    {
        if (!OperatingSystem.IsLinux()) return;
        System.IO.File.WriteAllText(Path.Combine(_root, "beside.txt"), "kept");
        var archive = Tar(File_("a/x.txt", "x"), File_("..", "over the parent"), File_("a/..", "over the folder"));
        await (await Into(archive, "root")).IsSuccess();
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "beside.txt"))).IsEqualTo("kept");
        await Assert.That(System.IO.File.ReadAllText(Path.Combine(_root, "root/a/x.txt"))).IsEqualTo("x");
    }

    [Test]
    public async Task MoreThanTheCap_StopsTheUnpack()
    {
        var unpacked = await Into(Tar(File_("big.txt", new string('x', 4096))), "root", max: 1000);
        await unpacked.IsFailure();
        await Assert.That(unpacked.Error!.Key).IsEqualTo("ArchiveTooLarge");
    }
}
