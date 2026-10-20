namespace PLang.Tests.App.actions.screen;

/// <summary>
/// PlangOS's errors, kept on the host: what the shell wrote to its errorlog channel comes up as json (kind 14), and the
/// host's window keeps each in errors.jsonl in the app's folder as a record — when, where, the error — written by the
/// json writer, one per line. What doesn't read as json is kept as its text, the error itself.
/// </summary>
public class ErrorsKeptTests : IDisposable
{
    private readonly string _root;
    private readonly global::app.@this _app;

    public ErrorsKeptTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_errors_" + Guid.NewGuid().ToString("N"))).FullName;
        _app = global::PLang.Tests.Session.App(_root).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    [Test]
    public async Task AnErrorThatCameUp_IsKept_AsARecordPerLine()
    {
        var errors = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();
        errors.Writer.TryWrite(System.Text.Encoding.UTF8.GetBytes("{\"error\":{\"key\":\"NotFound\",\"message\":\"no such file\"},\"where\":{\"goal\":\"FirstWords\"}}"));
        errors.Writer.TryWrite(System.Text.Encoding.UTF8.GetBytes("not json at all"));
        errors.Writer.TryComplete();

        await global::app.module.screen.type.screen.window.@this.Kept(errors.Reader, _app.actor.list.User.Context);

        var lines = File.ReadAllLines(Path.Combine(_root, "errors.jsonl"));
        await Assert.That(lines.Length).IsEqualTo(2);
        using var first = System.Text.Json.JsonDocument.Parse(lines[0]);
        await Assert.That(first.RootElement.GetProperty("at").GetString()).IsNotNull();
        await Assert.That(first.RootElement.GetProperty("error").GetProperty("key").GetString()).IsEqualTo("NotFound");
        await Assert.That(first.RootElement.GetProperty("where").GetProperty("goal").GetString()).IsEqualTo("FirstWords");
        using var second = System.Text.Json.JsonDocument.Parse(lines[1]);
        await Assert.That(second.RootElement.GetProperty("error").GetString()).IsEqualTo("not json at all");
    }
}
