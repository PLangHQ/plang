using PLang.Tests.App.Types.PathTests.Contract;

namespace PLang.Tests.App.actions.browser;

/// <summary>
/// What Chromium says on its own comes to each listener in the order it said it, one at a time — a page's video
/// chunks, a window's news: a listener that takes its time still sees them in order, and the pipe keeps being read.
/// </summary>
public class CdpEventTests : IDisposable
{
    private readonly string _root;
    private readonly global::app.@this _app;

    public CdpEventTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "plang_cdp_" + Guid.NewGuid().ToString("N"))).FullName;
        _app = new global::app.@this(_root).Testing();
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Directory.Delete(_root, true);
    }

    [Test]
    public async Task EventsReachAListener_InTheOrderSaid_EvenWhenItTakesItsTime()
    {
        const int count = 200;
        var said = string.Concat(Enumerable.Range(0, count).Select(i => "{\"method\":\"Runtime.bindingCalled\",\"params\":{\"n\":" + i + "}}\0"));
        var pipe = new global::app.channel.type.stream.@this("pipe", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(said)), ownsStream: true)
            { Framed = true, Actor = _app.actor.list.User.Context.Actor! };
        var heard = new List<int>();
        var all = new TaskCompletionSource();
        var random = new Random(1);
        var cdp = new global::app.module.browser.type.cdp.@this(pipe);
        cdp.Heard += async (method, parameters, session) =>
        {
            await Task.Delay(random.Next(0, 3));   // a listener that takes its time
            lock (heard)
            {
                heard.Add(parameters.GetProperty("n").GetInt32());
                if (heard.Count == count) all.TrySetResult();
            }
        };
        await all.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(heard).IsEquivalentTo(Enumerable.Range(0, count), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
