using PLang.Tests.App.Types.PathTests.Contract;
using Browser = app.module.browser.type.browser.@this;

namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// The browser is started trusted (a goal of plang's own starts Chromium), so a page it is sent to must not read for
/// the caller what the caller couldn't read itself (finding 1 of 579): a <c>file://</c> page is read as the caller —
/// asked when it is outside the caller's root, as any read is — and a website is the browser's normal job.
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

    [Test]
    public async Task AFilePageOutsideTheCallersRoot_IsAsked()
    {
        var refused = await Browser.Readable(new Uri(Path.Combine(_outside, "secret.html")).AbsoluteUri, Context);
        await Assert.That(refused).IsNotNull().Because("a file outside the caller's root is read as the caller: asked, and the answer was no");
        await Assert.That(refused!.Error!.Key).IsEqualTo("PermissionDenied");
    }

    [Test]
    public async Task AFilePageInTheCallersRoot_AndAWebsite_AreNotAsked()
    {
        await Assert.That(await Browser.Readable(new Uri(Path.Combine(_root, "page.html")).AbsoluteUri, Context)).IsNull();
        await Assert.That(await Browser.Readable("https://mbl.is", Context)).IsNull();
    }
}
