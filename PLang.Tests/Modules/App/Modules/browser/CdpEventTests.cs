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
        _app = new global::app.@this(_root, global::PLang.Tests.Parent.Actor).Testing();
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
        // Chromium says nothing until the listener is there: the pipe is empty until it is, so no event is read (and
        // dropped, heard by no one) before the listener is added
        using var chromium = new System.IO.Pipes.AnonymousPipeServerStream(System.IO.Pipes.PipeDirection.Out);
        var pipe = new global::app.channel.type.stream.@this(_app.actor.list.User.Channel, "pipe",
            new System.IO.Pipes.AnonymousPipeClientStream(System.IO.Pipes.PipeDirection.In, chromium.ClientSafePipeHandle), ownsStream: true)
            { Framed = true };
        var heard = new List<int>();
        var all = new TaskCompletionSource();
        var random = new Random(1);
        var cdp = new global::app.module.browser.type.cdp.@this(pipe);
        cdp.Heard += async (method, parameters, session) =>
        {
            // a listener that takes its time: it gives up its thread a few times before it hears, so it is still busy
            // when the next event is read — yielding, never sleeping, so the bound below only ever means a hang
            for (var turns = random.Next(0, 4); turns > 0; turns--) await Task.Yield();
            lock (heard)
            {
                heard.Add(parameters.GetProperty("n").GetInt32());
                if (heard.Count == count) all.TrySetResult();
            }
        };
        await chromium.WriteAsync(System.Text.Encoding.UTF8.GetBytes(said));
        await chromium.FlushAsync();
        await all.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(heard).IsEquivalentTo(Enumerable.Range(0, count), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
