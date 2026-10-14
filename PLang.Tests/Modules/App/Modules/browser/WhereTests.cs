using OnScreen = app.module.browser.type.browser.screen.@this;
using Page = app.module.browser.type.page.@this;

namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// A page is named as plang names it — a path from the app's root (<c>/app/writer/start.html</c>), never
/// <c>file://</c> — and a window says its page the same way: the page goes to Chromium as its file's address, and the
/// address comes back as the path. Typed in an address bar, a name with a dot is a website, words a search.
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
    public async Task APlangPath_IsItsFilesAddress_AndComesBackAsThePath()
    {
        var made = new global::app.data.@this("Url", (global::app.type.item.text.@this)"/app/writer/start.html", context: Context);
        var page = (await made.Value<Page>())!;
        await Assert.That(page.ToString()).IsEqualTo("/app/writer/start.html");
        var address = (await (await page.Address(Context)).Value())!.ToString();
        await Assert.That(address).IsEqualTo(new Uri(Path.Combine(_root, "app", "writer", "start.html")).AbsoluteUri);
        await Assert.That(OnScreen.Shown(address, Context)).IsEqualTo("/app/writer/start.html")
            .Because("a window says its page as plang names it, never file://");
    }

    private async Task<string?> TypedAs(string typed) => (await Page.Typed(typed, Context).Value())?.ToString();

    [Test]
    public async Task Typed_ANameWithADot_IsAWebsite_Words_ASearch_APath_ItsPage()
    {
        await Assert.That(await TypedAs("mbl.is")).IsEqualTo("https://mbl.is");
        await Assert.That(await TypedAs("plang os")).IsEqualTo("https://duckduckgo.com/?q=plang%20os");
        await Assert.That(await TypedAs("/app/writer/start.html")).IsEqualTo("/app/writer/start.html");
        await Assert.That(await TypedAs("https://mbl.is/x#part")).IsEqualTo("https://mbl.is/x#part");
    }

    [Test]
    public async Task Typed_ASchemeThatIsNeitherAFileNorAWebsite_IsNotAPage()
    {
        foreach (var typed in new[] { "about:blank", "chrome://settings", "javascript:alert(1)", "data:text/html,<p>x</p>" })
        {
            var page = Page.Typed(typed, Context);
            await Assert.That(page.Success).IsFalse().Because($"{typed}: a browser here shows files and websites only");
            await Assert.That(page.Error!.Key).IsEqualTo("NotAPage");
        }
    }
}
