using HttpPath = global::app.type.path.http.@this;
using AppEngine = global::app.@this;
using Ctx = global::app.actor.context.@this;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// The AuthGate prompt warns when the URL it is about to persist carries
/// a query string. Without the hint, a user who would strip <c>?token=…</c>
/// before pasting the URL into an issue tracker has no signal that 'a'
/// will save it verbatim to the local permission store. These tests
/// capture the rendered prompt and pin the wording.
/// </summary>
public class HttpPathPromptHintTests
{
    private sealed class CapturingChannel : global::app.channel.@this
    {
        public string LastQuestion = "";
        public string Answer = "n";

        public CapturingChannel()
        {
            Name = "input";
            Direction = global::app.channel.ChannelDirection.Bidirectional;
        }

        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());

        public override Task<global::app.data.@this> Read(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));

        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        {
            LastQuestion = action.Question.Value ?? "";
            return Task.FromResult(global::app.data.@this.Ok(Answer));
        }
    }

    private static (AppEngine app, Ctx context, CapturingChannel ch) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-http-hint-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new AppEngine(dir);
        var ch = new CapturingChannel();
        app.User.Channel.Register(ch);
        return (app, app.User.Context, ch);
    }

    [Test]
    public async Task Prompt_WithQueryString_IncludesPersistenceWarning()
    {
        var (_, context, ch) = MakeApp();
        // Use a host the gate won't auto-grant (out of root, not loopback-magic).
        var url = "https://api.example.com/files?token=secret123abc";

        _ = await new HttpPath(url, context).ReadText();

        await Assert.That(ch.LastQuestion).Contains("query string");
        await Assert.That(ch.LastQuestion).Contains("'a'");
    }

    [Test]
    public async Task Prompt_WithoutQueryString_OmitsPersistenceWarning()
    {
        var (_, context, ch) = MakeApp();
        var url = "https://api.example.com/files";

        _ = await new HttpPath(url, context).ReadText();

        await Assert.That(ch.LastQuestion).DoesNotContain("query string");
    }

    [Test]
    public async Task Prompt_FilePath_OmitsHttpWarning()
    {
        var (_, context, ch) = MakeApp();
        // Out-of-root file path also goes through the same gate.
        var outOfRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8],
            "x.txt");

        _ = await new global::app.type.path.file.@this(outOfRoot, context).ReadText();

        await Assert.That(ch.LastQuestion).DoesNotContain("query string");
    }
}
