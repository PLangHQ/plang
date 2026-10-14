using Browser = app.module.browser.type.browser.@this;
using OnScreen = app.module.browser.type.browser.screen.@this;

namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// A page is given as plang names it — a path from the app's root (<c>/app/writer/start.html</c>), never
/// <c>file://</c> — and a window says its page the same way: the path goes to Chromium as that file's address, and the
/// address comes back as the path. An address and words to search for are as typed in an address field.
/// </summary>
public class WhereTests : IDisposable
{
    private readonly string _root;
    private readonly global::app.@this _app;

    public WhereTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_where_" + Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(_root, "app", "writer"));
        File.WriteAllText(Path.Combine(_root, "app", "writer", "start.html"), "<p>mine</p>");
        _app = new global::app.@this(_root).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    [Test]
    public async Task APlangPath_IsThatFilesAddress_AndComesBackAsThePath()
    {
        var address = Browser.Where("/app/writer/start.html", Context);
        await Assert.That(address).IsEqualTo(new Uri(Path.Combine(_root, "app", "writer", "start.html")).AbsoluteUri);
        await Assert.That(OnScreen.Shown(address, Context)).IsEqualTo("/app/writer/start.html")
            .Because("a window says its page as plang names it, never file://");
    }

    [Test]
    public async Task AnAddress_AndWords_AreAsTyped()
    {
        await Assert.That(Browser.Where("https://mbl.is/x", Context)).IsEqualTo("https://mbl.is/x");
        await Assert.That(Browser.Where("mbl.is", Context)).IsEqualTo("https://mbl.is");
        await Assert.That(Browser.Where("plang os", Context)).IsEqualTo("https://duckduckgo.com/?q=plang%20os");
        await Assert.That(OnScreen.Shown("https://mbl.is/x", Context)).IsEqualTo("https://mbl.is/x");
    }
}
