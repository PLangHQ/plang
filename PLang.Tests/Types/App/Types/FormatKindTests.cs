namespace PLang.Tests.App.Types;

// A format is a kind of the type that reads it, found by its name, MIME or extension through one walk.
// Each test pins a way the old extension/MIME tables answered wrong.
public class FormatKindTests
{
    private static global::app.@this NewApp() => TestApp.Create("/test");

    [Test] public async Task MimeWithParameters_IsTheMediaType()
    {
        await using var app = NewApp();
        var type = app.type.list.Mime("application/json; charset=utf-8", app.User.Context);
        await Assert.That(type.Name).IsEqualTo("item");
        await Assert.That(type.kind.Name).IsEqualTo("json");
    }

    [Test] public async Task TextPlain_IsTextItself()
    {
        await using var app = NewApp();
        var type = app.type.list.Mime("text/plain", app.User.Context);
        await Assert.That(type.Name).IsEqualTo("text");
        await Assert.That(type.kind.IsEmpty).IsTrue();
    }

    [Test] public async Task TextHtml_IsCodesHtml_AndHtmIsTheSameFormat()
    {
        await using var app = NewApp();
        var byMime = app.type.list.Mime("text/html", app.User.Context);
        await Assert.That(byMime.Name).IsEqualTo("code");
        await Assert.That(byMime.kind.Name).IsEqualTo("html");
        await Assert.That(app.type.list.Extension(".htm", app.User.Context).kind.Name).IsEqualTo("html");
    }

    [Test] public async Task VideoMp4_IsMp4_AudioMp4_IsM4a()
    {
        await using var app = NewApp();
        var video = app.type.list.Mime("video/mp4", app.User.Context);
        var audio = app.type.list.Mime("audio/mp4", app.User.Context);
        await Assert.That(video.Name).IsEqualTo("binary");
        await Assert.That(video.kind.Name).IsEqualTo("mp4");
        await Assert.That(audio.kind.Name).IsEqualTo("m4a");
        await Assert.That(app.type.list.Extension(".mp4", app.User.Context).kind.Name).IsEqualTo("mp4");
    }

    [Test] public async Task AMimeTakenByAnotherFormat_IsRefusedAtRegistration()
    {
        // One format per MIME — the answer can't depend on the order the types came in.
        await using var app = NewApp();
        await Assert.That(() => app.type.list.Add(new global::app.type.kind.@this(
                new global::app.Attributes.FormatAttribute("plaintext", "text/plain", ".plaintext"), "binary")))
            .Throws<InvalidOperationException>();
    }

    [Test] public async Task UnknownMime_IsOpaqueBytes()
    {
        await using var app = NewApp();
        var type = app.type.list.Mime("application/x-unheard-of", app.User.Context);
        await Assert.That(type.Name).IsEqualTo("binary");
        await Assert.That(type.kind.IsEmpty).IsTrue();
    }

    [Test] public async Task Compressible_IsTheFormatsFact()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        await Assert.That(app.type.list.Mime("image/png", ctx).kind.Compressible).IsFalse();
        await Assert.That(app.type.list.Mime("video/mp4", ctx).kind.Compressible).IsFalse();
        await Assert.That(app.type.list.Mime("application/pdf", ctx).kind.Compressible).IsTrue();
        await Assert.That(app.type.list.Mime("text/plain", ctx).kind.Compressible).IsTrue();
        // a type with no format of its own says nothing compresses
        await Assert.That(app.type.list["dict"].kind.Compressible).IsFalse();
    }

    [Test] public async Task ATypesFormats_AreItsKindsThatHaveAMimeOrExtension()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var text = app.type.list["text"].format.list.Select(k => k.Name).ToList();
        await Assert.That(text).Contains("");       // its own format: plain text
        await Assert.That(text).Contains("md");
        await Assert.That(text).Contains("ini");
        await Assert.That(app.type.list["text"].format.list.First(k => k.IsEmpty).Mime).Contains("text/plain");
        // number has kinds (its precisions) but reads no format
        await Assert.That(app.type.list["number"].format.list.Count).IsEqualTo(0);
        await Assert.That(ctx).IsNotNull();
    }

    [Test] public async Task ATypesFormats_AreReachedFromPlang()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var read = await new global::app.type.item.variable.parser.@this("%!app.type.image.format.list%").Variable.Single().Start(ctx);
        await read.IsSuccess();
        var formats = new List<string>();
        foreach (var (_, entry) in await read.EnumerateItems())
            formats.Add((await (await entry.Get("name")).Value())!.ToString()!);
        await Assert.That(formats).Contains("png");
        await Assert.That(formats).Contains("jpg");
    }

    [Test] public async Task AFormatWithNoMimeOfItsOwn_ArrivesAsItsTypes()
    {
        await using var app = NewApp();
        var ini = global::app.type.item.path.@this.Resolve("/settings.ini", app.User.Context)!;
        await Assert.That(ini.MimeType(app.User.Context)).IsEqualTo("text/plain");
        await Assert.That(ini.Kind(app.User.Context).Name).IsEqualTo("text");
    }
}
