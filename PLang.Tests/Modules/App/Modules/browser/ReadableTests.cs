using PLang.Tests.App.Types.PathTests.Contract;
using Page = app.module.browser.type.page.@this;

namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// The browser is started trusted (a goal of plang's own starts Chromium), so a page it is sent to must not read for
/// the caller what the caller couldn't read itself (finding 1 of 579): a file page is read as the caller first — asked
/// when it is outside the caller's root, as any read is — and only then has an address. A website is the browser's
/// job: its url goes as written, unasked (decision 759).
/// </summary>
public class ReadableTests : IDisposable
{
    private readonly string _root;
    private readonly string _outside;
    private readonly global::app.@this _app;

    public ReadableTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_readable_" + Guid.NewGuid().ToString("N"))).FullName;
        _outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_readable_out_" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(_root, "page.html"), "<p>mine</p>");
        File.WriteAllText(Path.Combine(_outside, "secret.html"), "<p>not mine</p>");
        _app = new global::app.@this(_root).Testing();
        // each ask here is answered no: a page that asks is refused
        _app.actor.list.User.Context.Actor!.Channel.Register(new CannedAnswerChannel("n"));
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
        Directory.Delete(_outside, true);
    }

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    private async Task<Page> PageOf(string text)
    {
        var made = new global::app.data.@this("Url", (global::app.type.item.text.@this)text, context: Context);
        return (await made.Value<Page>())!;
    }

    [Test]
    public async Task AFilePageOutsideTheCallersRoot_IsAsked_AndHasNoAddress()
    {
        var page = await PageOf(new Uri(Path.Combine(_outside, "secret.html")).AbsoluteUri);
        var address = await page.Address(Context);
        await Assert.That(address.Success).IsFalse().Because("a file outside the caller's root is read as the caller: asked, and the answer was no");
        await Assert.That(address.Error!.Key).IsEqualTo("PermissionDenied");
    }

    [Test]
    public async Task AFilePageInTheRoot_IsItsFile_AndAWebsite_IsItsUrlAsWritten_Unasked()
    {
        var mine = await (await PageOf("/page.html")).Address(Context);
        await Assert.That(mine.Success).IsTrue();
        await Assert.That((await mine.Value())!.ToString()).IsEqualTo(new Uri(Path.Combine(_root, "page.html")).AbsoluteUri);
        var site = await (await PageOf("https://mbl.is/x?b=2&a=1#part")).Address(Context);
        await Assert.That(site.Success).IsTrue().Because("a website is the browser's job, not plang reading the web: nothing is asked");
        await Assert.That((await site.Value())!.ToString()).IsEqualTo("https://mbl.is/x?b=2&a=1#part")
            .Because("its query and #part go as written");
    }
}
