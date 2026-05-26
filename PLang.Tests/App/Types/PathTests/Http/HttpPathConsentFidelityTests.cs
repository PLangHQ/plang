using HttpPath = global::app.types.path.http.@this;
using AppEngine = global::app.@this;
using Ctx = global::app.actor.context.@this;
using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.Types.PathTests.Http;

/// <summary>
/// The consent contract must be airtight: the URL the user sees in the
/// AuthGate prompt must equal the URL we fetch and the URL we persist as
/// the grant key. Two divergences to guard against:
///   – IDN homograph: <c>_uri.Host</c> returning Unicode lets
///     <c>аpple.com</c> (Cyrillic 'а') render as the trusted brand;
///   – embedded userinfo: <c>https://user:pwd@victim/</c> rendering as
///     <c>https://victim/</c> while the wire still carries userinfo,
///     making a single grant cover any userinfo-bearing variant.
/// </summary>
public class HttpPathConsentFidelityTests
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
        public override Task<global::app.data.@this> WriteCore(global::app.data.@this d, CancellationToken ct = default)
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
            "plang-http-consent-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new AppEngine(dir);
        var ch = new CapturingChannel();
        app.User.Channels.Register(ch);
        return (app, app.User.Context, ch);
    }

    // --- S4.a -----------------------------------------------------------------

    [Test]
    public async Task IdnHost_HomographHost_RendersAsPunycodeInPromptAndAbsolute()
    {
        var (_, ctx, ch) = MakeApp();
        // Cyrillic 'а' (U+0430) + Latin "pple.com" — looks like apple to a human,
        // but IdnHost is xn--pple-43d.com.
        var url = "https://аpple.com/login";

        // Read so Authorize fires and renders the prompt.
        _ = await new HttpPath(url, ctx).ReadText();

        await Assert.That(ch.LastQuestion).Contains("xn--pple-43d.com");
        await Assert.That(ch.LastQuestion).DoesNotContain("аpple.com");

        // Absolute is what gets persisted as the grant key. Same canonical form.
        var path = new HttpPath(url, ctx);
        await Assert.That(path.Absolute).Contains("xn--pple-43d.com");
    }

    [Test]
    public async Task IdnHost_AsciiHost_RendersUnchanged()
    {
        var (_, ctx, ch) = MakeApp();
        var url = "https://apple.com/login";

        _ = await new HttpPath(url, ctx).ReadText();

        await Assert.That(ch.LastQuestion).Contains("apple.com");
        await Assert.That(ch.LastQuestion).DoesNotContain("xn--");
    }

    // --- S4.b -----------------------------------------------------------------

    [Test]
    public async Task UserInfo_StrippedAtConstruction_AbsoluteOmitsCredentials()
    {
        var (_, ctx, _) = MakeApp();
        var url = "https://attacker:pwd@victim.example/admin";
        var path = new HttpPath(url, ctx);

        await Assert.That(path.Absolute).DoesNotContain("attacker");
        await Assert.That(path.Absolute).DoesNotContain("pwd");
        await Assert.That(path.Absolute).DoesNotContain("@victim.example");
        await Assert.That(path.Absolute).IsEqualTo("https://victim.example/admin");
    }

    [Test]
    public async Task UserInfo_StrippedAtConstruction_UriOnWireOmitsCredentials()
    {
        var (_, ctx, _) = MakeApp();
        // Uri.ToString should also have no userinfo since we rebuilt _uri.
        var url = "https://u:p@victim.example/path";
        var path = new HttpPath(url, ctx);

        await Assert.That(path.Uri.UserInfo).IsEqualTo("");
        await Assert.That(path.Uri.ToString()).DoesNotContain("@victim.example");
    }

    [Test]
    public async Task UserInfo_PromptShowsCleanUrl_AndGrantKeyIsClean()
    {
        var (_, ctx, ch) = MakeApp();
        var url = "https://attacker:pwd@victim.example/admin";

        _ = await new HttpPath(url, ctx).ReadText();

        await Assert.That(ch.LastQuestion).DoesNotContain("attacker");
        await Assert.That(ch.LastQuestion).DoesNotContain("pwd");
        await Assert.That(ch.LastQuestion).Contains("https://victim.example/admin");
    }
}
