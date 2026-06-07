using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using filechannel = global::app.channel.type.file.@this;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// The file channel — a filesystem channel kind. Mime is derived from the
// extension. Bytes come from `path.ReadBytes` (which holds the AuthGate),
// so the channel does no System.IO of its own — PLNG002 stays clean.
public class FileChannelTests
{
    private static global::app.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-filechan-" + System.Guid.NewGuid().ToString("N")[..8]);
        return new(root);
    }

    // The channel reads through path.ReadBytes — the gated verb surface — never
    // System.IO directly. An in-root read the gate permits returns the file's
    // content (lazily, stamped from the extension).
    [Test] public async Task FileChannel_ReadsBytesViaPathReadBytes_AuthGateEnforced()
    {
        await using var app = NewApp(out var root);
        var p = new filepath(System.IO.Path.Combine(root, "data.json"), app.User.Context);
        await (await p.WriteText("{\"port\":8080}")).IsSuccess();

        var ch = new filechannel(p);
        var d = await ch.Read();
        await d.IsSuccess();
        await Assert.That(d.HasRaw).IsTrue();
        await Assert.That(d.Raw).IsEqualTo((object)"{\"port\":8080}");
    }

    [Test] public async Task FileChannel_Mime_DerivedFromExtension()
    {
        await using var app = NewApp(out var root);
        var json = new filechannel(new filepath(System.IO.Path.Combine(root, "x.json"), app.User.Context));
        var csv = new filechannel(new filepath(System.IO.Path.Combine(root, "x.csv"), app.User.Context));
        await Assert.That(json.Mime).IsEqualTo("application/json");
        await Assert.That(csv.Mime).IsEqualTo("text/csv");
    }

    // file.read opens the file channel and reads — no read-time
    // `Context.App.Type.Convert(...)`. The result is LAZY: raw set, value
    // unmaterialized, so the conversion that ReadText used to do at read time
    // is gone (it now fires on first touch).
    [Test] public async Task FileRead_OpensFileChannel_NoReadTimeConvertInFilePathReadText()
    {
        await using var app = NewApp(out var root);
        var p = new filepath(System.IO.Path.Combine(root, "cfg.json"), app.User.Context);
        await (await p.WriteText("{\"port\":8080}")).IsSuccess();

        var ch = new filechannel(p);
        var d = await ch.Read();
        await Assert.That(d.MaterializeCount).IsEqualTo(0); // nothing parsed at read time
        await Assert.That(d.Type.Name).IsEqualTo("item");
        await Assert.That(d.Type.Kind).IsEqualTo("json");
    }
}
