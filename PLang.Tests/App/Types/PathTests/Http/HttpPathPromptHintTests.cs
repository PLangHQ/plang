using HttpPath = global::app.types.path.http.@this;
using AppEngine = global::app.@this;
using Ctx = global::app.actor.context.@this;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// Security v1 S3 — the AuthGate prompt now warns when the URL it is about
/// to persist carries a query string. Without the hint a user who would
/// strip <c>?token=…</c> before pasting the URL into an issue tracker has
/// no signal that 'a' will save it verbatim to the local permission store.
/// These tests capture the rendered prompt and pin the wording.
/// </summary>
public class HttpPathPromptHintTests
{
    private sealed class CapturingChannel : global::app.channels.channel.@this
    {
        public string LastQuestion = "";
        public string Answer = "n";

        public CapturingChannel()
        {
            Name = "input";
            Direction = global::app.channels.channel.ChannelDirection.Bidirectional;
        }

        public override Task<global::app.data.@this> WriteCore(global::app.data.@this data, CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok());

        public override Task<global::app.data.@this> ReadCore(CancellationToken ct = default)
            => Task.FromResult(global::app.data.@this.Ok((object?)null));

        public override Task<global::app.data.@this> AskCore(global::app.modules.output.ask action, CancellationToken ct = default)
        {
            LastQuestion = action.Question.Value ?? "";
            return Task.FromResult(global::app.data.@this.Ok(Answer));
        }
    }

    private static (AppEngine app, Ctx ctx, CapturingChannel ch) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-http-hint-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new AppEngine(dir);
        var ch = new CapturingChannel();
        app.User.Channels.Register(ch);
        return (app, app.User.Context, ch);
    }

    [Test]
    public async Task Prompt_WithQueryString_IncludesPersistenceWarning()
    {
        var (_, ctx, ch) = MakeApp();
        // Use a host the gate won't auto-grant (out of root, not loopback-magic).
        var url = "https://api.example.com/files?token=secret123abc";

        _ = await new HttpPath(url, ctx).ReadText();

        await Assert.That(ch.LastQuestion).Contains("query string");
        await Assert.That(ch.LastQuestion).Contains("'a'");
    }

    [Test]
    public async Task Prompt_WithoutQueryString_OmitsPersistenceWarning()
    {
        var (_, ctx, ch) = MakeApp();
        var url = "https://api.example.com/files";

        _ = await new HttpPath(url, ctx).ReadText();

        await Assert.That(ch.LastQuestion).DoesNotContain("query string");
    }

    [Test]
    public async Task Prompt_FilePath_OmitsHttpWarning()
    {
        var (_, ctx, ch) = MakeApp();
        // Out-of-root file path also goes through the same gate.
        var outOfRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8],
            "x.txt");

        _ = await new global::app.types.path.file.@this(outOfRoot, ctx).ReadText();

        await Assert.That(ch.LastQuestion).DoesNotContain("query string");
    }
}
