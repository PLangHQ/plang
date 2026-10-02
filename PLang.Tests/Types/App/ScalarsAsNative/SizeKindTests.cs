using Size = global::app.type.item.size.@this;

namespace PLang.Tests.App.ScalarsAsNative;

// A size's standards are its kinds (iec 95.4 MiB, si 100 MB): a size read from text is born with the one its suffix
// names and writes back in it; a size made from a count is written in the standard its asker's setting names.
public class SizeKindTests
{
    private static async Task<global::app.data.@this> Read(global::app.actor.context.@this context, string path)
        => await new global::app.type.item.variable.parser.@this(path).Variable.Single().Start(context);

    [Test]
    [Arguments("100 MB", "si", 100_000_000L)]
    [Arguments("100mb", "si", 100_000_000L)]
    [Arguments("512 KB", "si", 512_000L)]
    [Arguments("100 MiB", "iec", 104_857_600L)]
    [Arguments("100mib", "iec", 104_857_600L)]
    [Arguments("1.5 GiB", "iec", 1_610_612_736L)]
    public async Task TheSuffix_NamesTheStandard(string text, string kind, long bytes)
    {
        var size = Size.Resolve(text, null)!;

        await Assert.That(size.Value).IsEqualTo(bytes);
        await Assert.That(new global::app.data.@this("s", size).Type.kind.Name).IsEqualTo(kind);
    }

    [Test]
    [Arguments("100 MB")]
    [Arguments("512 kB")]
    [Arguments("95.4 MiB")]
    [Arguments("500 KiB")]
    [Arguments("2 GiB")]
    public async Task ASizeReadFromText_WritesItBack(string text)
        => await Assert.That(Size.Resolve(text, null)!.Text).IsEqualTo(text);

    [Test]
    [Arguments(512L, "512 B")]
    [Arguments(1024L, "1 KiB")]
    [Arguments(100_000_000L, "95.4 MiB")]
    [Arguments(0L, "0 B")]
    public async Task ACountWithNoAsker_IsIec_InItsLargestUnit(long bytes, string text)
        => await Assert.That(Size.Create(bytes)!.Text).IsEqualTo(text);

    [Test]
    [Arguments("12 parsecs")]
    [Arguments("MB")]
    [Arguments("1.5")]
    public async Task ATextInNoStandard_IsNoSize(string text)
        => await Assert.That(Size.Resolve(text, null)).IsNull();

    [Test]
    public async Task OneSizeInTwoStandards_IsEqual_ByItsBytes()
        => await Assert.That(Size.Resolve("1 MB", null)!.Equals(Size.Resolve("1000 kB", null))).IsTrue();

    // The setting names the standard a counted size is written in: si, set for this run, writes 100000000 as 100 MB.
    [Test]
    public async Task TheSetting_NamesACountsStandard()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;

        var set = await new global::app.type.item.variable.parser.@this("%!app.type.size.setting.standard%").Variable.Single()
            .Set(context.Ok(new global::app.type.item.text.@this("si")), context);
        var standard = await Read(context, "%!app.type.size.setting.standard%");

        await set.IsSuccess();
        await Assert.That((await standard.Value())?.ToString()).IsEqualTo("si");
        await Assert.That(new Size(100_000_000L, context).Text).IsEqualTo("100 MB");
    }

    [Test]
    public async Task WithNothingSet_ACountIsIec()
    {
        await using var app = new global::app.@this("/app").Testing();

        await Assert.That(new Size(100_000_000L, app.actor.list.User.Context).Text).IsEqualTo("95.4 MiB");
    }

    // `as si`: the declared type names the kind, and the same bytes are written in it.
    [Test]
    public async Task AKindAskedFor_WritesTheBytesInIt()
    {
        await using var app = new global::app.@this("/app").Testing();
        var carrier = new global::app.data.@this("s", context: app.actor.list.User.Context);

        var converted = Size.Create("100 MiB", new global::app.type.@this("size", "si"), carrier)!;

        await Assert.That(converted.Text).IsEqualTo("104.9 MB");
        await Assert.That(converted.Value).IsEqualTo(104_857_600L);
    }

    [Test]
    public async Task ASize_ComparesWithANumber_AsBytes()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var size = new global::app.data.@this("s", Size.Resolve("1 KiB", null)!, context: context);

        await Assert.That(await size.Compare(new global::app.data.@this("n", (global::app.type.item.number.@this)2048L, context: context)))
            .IsEqualTo(global::app.data.Comparison.Less);
    }

    // A file's size is a size, born with its asker's standard.
    [Test]
    public async Task AFilesSize_IsASize()
    {
        var root = System.IO.Directory.CreateTempSubdirectory("plang_size_").FullName;
        try
        {
            await using var app = new global::app.@this(root).Testing();
            var context = app.actor.list.User.Context;
            var file = (global::app.type.item.path.file.@this)global::app.type.item.path.@this.Resolve("/a.bin", context);
            await (await file.WriteBytes(new byte[2048], context)).IsSuccess();

            var size = await (await file.Size(context)).Value();

            await Assert.That(size!.Value).IsEqualTo(2048L);
            await Assert.That(size.Text).IsEqualTo("2 KiB");
        }
        finally { System.IO.Directory.Delete(root, true); }
    }
}
