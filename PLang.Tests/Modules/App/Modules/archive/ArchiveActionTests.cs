using Archive = global::app.module.archive.type.archive.@this;
using Kind = global::app.module.archive.type.archive.kind.@this;

namespace PLang.Tests.App.Modules.archive;

/// <summary>
/// archive.pack and archive.unpack: the value decides what is packed (a Data whole, a file's contents and name, a folder
/// as a bundle), the archive says what it holds, and unpacking gives that back — a Data as the Data, a file or a folder
/// into a folder.
/// </summary>
public class ArchiveActionTests : IDisposable
{
    private readonly string _root;
    private readonly global::app.@this _app;

    public ArchiveActionTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_archive_" + Guid.NewGuid().ToString("N"))).FullName;
        _app = new global::app.@this(_root).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private string OnDisk(string plang) => Path.Combine(_root, plang.TrimStart('/'));

    private global::app.data.@this<global::app.type.item.choice.@this<Kind>> Format(string name)
        => new global::app.data.@this("", name, context: Ctx).As<global::app.type.item.choice.@this<Kind>>();

    private global::app.data.@this<global::app.type.item.path.@this> At(string plang)
        => global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(plang, Ctx));

    private async Task<global::app.data.@this> Pack(global::app.data.@this value, string? format = null, string? to = null)
    {
        var action = new global::app.module.archive.pack(Ctx)
        {
            Value = value,
            Format = format == null ? null : Format(format),
            To = to == null ? null : At(to),
        };
        await action.Attach(null, Ctx);
        return await action.Start();
    }

    private async Task<global::app.data.@this> Unpack(object archive, string? into = null, string? format = null, long? max = null)
    {
        var action = new global::app.module.archive.unpack(Ctx)
        {
            Value = global::app.data.@this<Archive>.From(new global::app.data.@this("archive", archive, context: Ctx)),
            Into = into == null ? null : At(into),
            Format = format == null ? null : Format(format),
            Max = max == null ? global::app.data.@this<global::app.type.item.size.@this>.Uninitialized("max") : new global::app.type.item.size.@this(max.Value, Ctx),
        };
        await action.Attach(null, Ctx);
        return await action.Start();
    }

    private global::app.data.@this Text(string name, string value)
        => new(name, value, _app.type.list.Stamp("text/plain", Ctx), context: Ctx);

    // ---- a value: packed as its Data whole, unpacked back to it ----

    [Test]
    [Arguments("gzip")]
    [Arguments("deflate")]
    [Arguments("brotli")]
    public async Task AValue_RoundTrips_InEachCompression(string format)
    {
        var packed = await Pack(Text("note", "The quick brown fox jumps over the lazy dog"), format);

        await packed.IsSuccess();
        var archive = (Archive)(await packed.Value())!;
        await Assert.That(archive.Format!.Name).IsEqualTo(format);
        await Assert.That(archive.Held!.IsData).IsTrue();
        var restored = await Unpack(archive);
        await restored.IsSuccess();
        await Assert.That((await restored.Value())?.ToString()).IsEqualTo("The quick brown fox jumps over the lazy dog");
    }

    // Packing writes the Data whole in plang's own format, signed as it leaves (sign-if-missing); unpacking reads it
    // back and verifies that signature — with real crypto, the hash read back is the hash signed.
    [Test]
    public async Task ASignedValue_RoundTrips_WithRealSigning()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_archive_signed_" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            await using var app = new global::app.@this(root);
            app.test.list.Open();
            app.TestIdentity();
            var ctx = app.actor.list.User.Context;
            await ctx.Variable.Set("original", new global::app.data.@this("original", "The quick brown fox jumps over the lazy dog.",
                app.type.list.Stamp("text/plain", ctx), context: ctx));

            // as a step runs them: the value read from %original%, the archive kept in %archived% and read from there
            var packed = await ctx.Action("archive.pack(Value=%original%)").Start(ctx);
            await packed.IsSuccess();
            await ctx.Variable.Set("archived", packed);
            var restored = await ctx.Action("archive.unpack(Value=%archived%)").Start(ctx);

            await restored.IsSuccess();
            await Assert.That((await restored.Value())?.ToString()).IsEqualTo("The quick brown fox jumps over the lazy dog.");
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public async Task NoFormatNamed_IsGzip()
    {
        var archive = (Archive)(await (await Pack(Text("note", "hi"))).Value())!;

        await Assert.That(archive.Format!.Name).IsEqualTo("gzip");
        await Assert.That(new global::app.data.@this("a", archive, context: Ctx).Type.kind.Name).IsEqualTo("gzip");
    }

    // The value and its properties come back; its name is the variable it is written to, never the archive's.
    [Test]
    public async Task AValuesProperties_SurviveTheRoundTrip()
    {
        var value = Text("note", "Hello");
        value.Properties["metadata"] = "some value";

        var restored = await Unpack((await (await Pack(value)).Value())!);

        await restored.IsSuccess();
        await Assert.That((await restored.Value())?.ToString()).IsEqualTo("Hello");
        await Assert.That((await restored.Properties.Value("metadata"))?.ToString()).IsEqualTo("some value");
    }

    [Test]
    public async Task CorruptBytes_AreRefused()
    {
        var archive = new Archive([0xFF, 0xFE, 0x00, 0x42], (Kind)_app.type.list["archive"].kind["gzip"]!,
            new global::app.module.archive.type.archive.held.@this("data"));

        var restored = await Unpack(archive);

        await restored.IsFailure();
        await Assert.That(restored.Error!.Key).IsEqualTo("UnpackFailed");
    }

    [Test]
    public async Task MoreThanItsMax_IsRefused_AsItPassesIt()
    {
        var archive = (Archive)(await (await Pack(Text("big", new string('x', 10_000)))).Value())!;

        var restored = await Unpack(archive, max: 1000);

        await restored.IsFailure();
        await Assert.That(restored.Error!.Key).IsEqualTo("ArchiveTooLarge");
    }

    [Test]
    public async Task AnArchive_RidesTheWire_AsWhatItHolds()
    {
        var archive = (await (await Pack(Text("note", "wired"))).Value())!;
        var data = new global::app.data.@this("archived", archive, context: Ctx);
        using var wire = new MemoryStream();
        await (await _app.type.list["wire"].kind["plang"]!.Encode(wire, data, Ctx)).IsSuccess();

        var back = await _app.type.list["wire"].kind["plang"]!.Decode(wire.ToArray(), Ctx);

        await Assert.That(System.Text.Encoding.UTF8.GetString(wire.ToArray())).Contains("\"held\":{\"type\":\"data\"");
        await back.IsSuccess();
        var read = (Archive)(await back.Value())!;
        await Assert.That(read.Format!.Name).IsEqualTo("gzip");
        await Assert.That(read.Held!.IsData).IsTrue();
        await Assert.That((await (await Unpack(read)).Value())?.ToString()).IsEqualTo("wired");
    }

    // ---- a file: its contents and its name, unpacked into a folder ----

    [Test]
    public async Task AFile_PacksItsContentsAndName_AndUnpacksIntoAFolder()
    {
        File.WriteAllText(OnDisk("/a.txt"), "file content");
        var file = new global::app.data.@this("f", global::app.type.item.path.@this.Resolve("/a.txt", Ctx), context: Ctx);

        var archive = (Archive)(await (await Pack(file)).Value())!;
        var unpacked = await Unpack(archive, into: "/out");

        await Assert.That(archive.Held!.Of.ToString()).IsEqualTo("file");
        await Assert.That(archive.Held.Name!.ToString()).IsEqualTo("a.txt");
        await unpacked.IsSuccess();
        await Assert.That(File.ReadAllText(OnDisk("/out/a.txt"))).IsEqualTo("file content");
    }

    // What an archive says it holds is the archive's word, never trusted: a file named ../escape.txt lands inside.
    [Test]
    public async Task AHeldFilesName_ClimbingOut_StaysInside()
    {
        var gz = new MemoryStream();
        using (var zip = new System.IO.Compression.GZipStream(gz, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            zip.Write("escaped?"u8);
        var archive = new Archive(gz.ToArray(), (Kind)_app.type.list["archive"].kind["gzip"]!,
            new global::app.module.archive.type.archive.held.@this("file", "../../escape.txt"));

        var unpacked = await Unpack(archive, into: "/out/deep");

        await unpacked.IsSuccess();
        await Assert.That(File.Exists(OnDisk("/escape.txt"))).IsFalse();
        await Assert.That(File.Exists(OnDisk("/out/escape.txt"))).IsFalse();
        await Assert.That(File.ReadAllText(OnDisk("/out/deep/escape.txt"))).IsEqualTo("escaped?");
    }

    [Test]
    public async Task AnArchivedFile_WithNoFolder_IsRefused()
    {
        File.WriteAllText(OnDisk("/a.txt"), "file content");
        var archive = (await (await Pack(new global::app.data.@this("f", global::app.type.item.path.@this.Resolve("/a.txt", Ctx), context: Ctx))).Value())!;

        var unpacked = await Unpack(archive);

        await unpacked.IsFailure();
        await Assert.That(unpacked.Error!.Key).IsEqualTo("UnpackNeedsFolder");
    }

    [Test]
    public async Task AGzFileAtRest_UnpacksAsTheFileItHolds()
    {
        using (var gz = new System.IO.Compression.GZipStream(File.Create(OnDisk("/notes.txt.gz")), System.IO.Compression.CompressionLevel.Optimal))
            gz.Write("from elsewhere"u8);

        var unpacked = await Unpack(global::app.type.item.path.@this.Resolve("/notes.txt.gz", Ctx), into: "/out");

        await unpacked.IsSuccess();
        await Assert.That(File.ReadAllText(OnDisk("/out/notes.txt"))).IsEqualTo("from elsewhere");
    }

    // ---- a folder: a bundle, its format named or by To's name ----

    private void Photos()
    {
        Directory.CreateDirectory(OnDisk("/photos/2024"));
        File.WriteAllText(OnDisk("/photos/a.jpg"), "a");
        File.WriteAllText(OnDisk("/photos/2024/b.jpg"), "b");
    }

    private global::app.data.@this Folder(string plang) => new("folder", global::app.type.item.path.@this.Resolve(plang, Ctx), context: Ctx);

    [Test]
    [Arguments("/backup/photos.tar.gz")]
    [Arguments("/backup/photos.tar")]
    [Arguments("/backup/photos.zip")]
    public async Task AFolder_PacksToAPath_InTheFormatItsNameEndsIn_AndUnpacksBack(string to)
    {
        Photos();

        var packed = await Pack(Folder("/photos"), to: to);
        var unpacked = await Unpack(global::app.type.item.path.@this.Resolve(to, Ctx), into: "/restored");

        await packed.IsSuccess();
        await unpacked.IsSuccess();
        await Assert.That(File.ReadAllText(OnDisk("/restored/a.jpg"))).IsEqualTo("a");
        await Assert.That(File.ReadAllText(OnDisk("/restored/2024/b.jpg"))).IsEqualTo("b");
    }

    // `pack /photos to …`: the step writes the folder as text; a bundle reads it as the path it names.
    [Test]
    public async Task AFolderWrittenAsText_IsTheFolderItNames()
    {
        Photos();

        var packed = await Pack(Text("folder", "/photos"), to: "/backup/photos.zip");
        var unpacked = await Unpack(global::app.type.item.path.@this.Resolve("/backup/photos.zip", Ctx), into: "/restored");

        await packed.IsSuccess();
        await unpacked.IsSuccess();
        await Assert.That(File.ReadAllText(OnDisk("/restored/2024/b.jpg"))).IsEqualTo("b");
    }

    [Test]
    public async Task AFolder_WithNoBundleNamed_IsRefused()
    {
        Photos();

        var packed = await Pack(Folder("/photos"));

        await packed.IsFailure();
        await Assert.That(packed.Error!.Key).IsEqualTo("PackNeedsBundle");
        await Assert.That(packed.Error.Message).Contains("name a bundle");
    }

    [Test]
    public async Task AFolder_PackedInMemory_UnpacksIntoAFolder()
    {
        Photos();

        var archive = (Archive)(await (await Pack(Folder("/photos"), "zip")).Value())!;
        var unpacked = await Unpack(archive, into: "/restored");

        await Assert.That(archive.Held!.Of.ToString()).IsEqualTo("folder");
        await unpacked.IsSuccess();
        await Assert.That(File.ReadAllText(OnDisk("/restored/2024/b.jpg"))).IsEqualTo("b");
    }

    [Test]
    public async Task AFormatNoOneReads_IsRefused_NamingItsFirstBytes()
    {
        File.WriteAllBytes(OnDisk("/layer.bin"), [0x28, 0xb5, 0x2f, 0xfd, 0, 0, 0, 0]);

        var unpacked = await Unpack(global::app.type.item.path.@this.Resolve("/layer.bin", Ctx), into: "/out");

        await unpacked.IsFailure();
        await Assert.That(unpacked.Error!.Key).IsEqualTo("UnpackNotSupported");
        await Assert.That(unpacked.Error.Message).Contains("28B52FFD");
    }

    [Test]
    public async Task TheSettingsMax_AppliesWhenTheStepSaysNone()
    {
        var archive = (Archive)(await (await Pack(Text("big", new string('x', 10_000)))).Value())!;
        await (await new global::app.type.item.variable.parser.@this("%!archive.setting.max%").Variable.Single()
            .Set(Ctx.Ok(new global::app.type.item.text.@this("1 kB")), Ctx)).IsSuccess();

        var restored = await Unpack(archive);

        await restored.IsFailure();
        await Assert.That(restored.Error!.Key).IsEqualTo("ArchiveTooLarge");
        await Assert.That(restored.Error.Message).Contains("1 kB");
    }
}
