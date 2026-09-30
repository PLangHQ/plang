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
