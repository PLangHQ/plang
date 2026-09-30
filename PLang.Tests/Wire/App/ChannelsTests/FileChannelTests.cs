using PLangFilePath = global::app.type.item.path.file.@this;

namespace PLang.Tests.App.ChannelsTests;

// A file is the channel a saved value is written to: the value is opened at the last moment and the file's
// extension names the format that writes it, in one write.
public class FileChannelTests
{
    private static (global::app.@this app, global::app.actor.context.@this context, string dir) MakeApp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "plang_filech_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir).Testing();
        return (app, app.actor.list.User.Context, dir);
    }

    [Test]
    public async Task SavingAReadJsonFile_IsByteIdentical()
    {
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        const string json = "{ \"port\":  8080,\n  \"hosts\": [\"a\", \"b\"] }";
        File.WriteAllText(Path.Combine(dir, "a.json"), json);

        var read = await new PLangFilePath(Path.Combine(dir, "a.json")).Read(context);
        var saved = await new PLangFilePath(Path.Combine(dir, "b.json")).Save(read, context);

        await saved.IsSuccess();
        await Assert.That(File.ReadAllText(Path.Combine(dir, "b.json"))).IsEqualTo(json);
    }

    // A 1x1 PNG, the image a binding on image create answers.
    private static readonly byte[] Png1x1 =
    {
        0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
        0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
        0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
        0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
        0x42,0x60,0x82
    };

    private static global::app.data.@this ADict(global::app.actor.context.@this context)
        => context.Ok(new Dictionary<string, object?> { ["a"] = 1L });

    [Test]
    public async Task SavingADict_ToPng_FailsWithTheImagesReason_NothingWritten()
    {
        var (app, context, dir) = MakeApp();
        await using var _ = app;

        var saved = await new PLangFilePath(Path.Combine(dir, "x.png")).Save(ADict(context), context);

        await saved.IsFailure();
        await Assert.That(saved.Error!.Message).Contains("not an image");
        await Assert.That(File.Exists(Path.Combine(dir, "x.png"))).IsFalse();
    }

    [Test]
    public async Task SavingADict_ToPng_WritesTheImageABindingOnImageCreateAnswers()
    {
        var (app, context, dir) = MakeApp();
        await using var _ = app;
        app.type.list["image"].Own().Bind("create", global::app.@event.When.before, (_, _, ctx) =>
        {
            global::app.data.@this made = ctx.Ok(new global::app.type.item.image.@this(Png1x1, "image/png"));
            made.Handled = true;
            return Task.FromResult(made);
        }, app.actor.list.User, global::app.@event.binding.Scope.actor);

        var saved = await new PLangFilePath(Path.Combine(dir, "x.png")).Save(ADict(context), context);

        await saved.IsSuccess();
        await Assert.That(File.ReadAllBytes(Path.Combine(dir, "x.png"))).IsEquivalentTo(Png1x1);
    }

    [Test]
    [Arguments("note.md")]
    [Arguments("note.log")]
    [Arguments("note.xyz")]
    public async Task SavingText_ToAFileOfAnotherFormat_IsTheText(string name)
    {
        var (app, context, dir) = MakeApp();
        await using var _ = app;

        var saved = await new PLangFilePath(Path.Combine(dir, name)).Save(context.Ok("hello"), context);

        await saved.IsSuccess();
        await Assert.That(File.ReadAllText(Path.Combine(dir, name))).IsEqualTo("hello");
    }

    [Test]
    public async Task SavingText_ToTxt_IsTheTextAlone()
    {
        var (app, context, dir) = MakeApp();
        await using var _ = app;

        var saved = await new PLangFilePath(Path.Combine(dir, "note.txt")).Save(context.Ok("hello"), context);

        await saved.IsSuccess();
        await Assert.That(File.ReadAllText(Path.Combine(dir, "note.txt"))).IsEqualTo("hello");
    }
}
