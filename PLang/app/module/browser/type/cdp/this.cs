using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace app.module.browser.type.cdp;

/// <summary>
/// One DevTools connection to a Chromium (the Chrome DevTools Protocol), over its <c>--remote-debugging-pipe</c>: the
/// pipe channel of the program that runs it (<c>%program.pipe%</c>), each message JSON ended by NUL. A request goes out
/// with an id and its reply comes back with it; what Chromium says on its own (an event) goes to whoever listens. The
/// whole browser speaks here, and each page through a session on it (<see cref="session.@this"/>) — no port, no
/// WebSocket. JSON is DevTools' own: it is read and written here, at its edge.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private readonly global::app.channel.type.stream.@this _pipe;
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _waiting = new();
    private int _next;

    /// <summary>Speaks DevTools over <paramref name="pipe"/>: its messages end with NUL from now on.</summary>
    internal @this(global::app.channel.type.stream.@this pipe)
    {
        _pipe = pipe;
        _pipe.End = "\0";
        Ended = Task.Run(Listen);
    }

    /// <summary>What Chromium said on its own: the event's method, its params, and the session it came from (null for
    /// the browser's own).</summary>
    internal event Func<string, JsonElement, string?, Task>? Heard;

    /// <summary>Done when the connection ends (Chromium closed its end).</summary>
    internal Task Ended { get; }

    /// <summary>A request — to the browser, or to the page of <paramref name="session"/> — and its whole reply
    /// (<c>{"id", "result"}</c> or <c>{"id", "error"}</c>); a timeout when none comes within <paramref name="within"/>
    /// (5 s unless said: a page's goal may wait for the person).</summary>
    internal async Task<JsonElement> Ask(string method, JsonObject parameters, string? session = null, TimeSpan? within = null)
    {
        var id = Interlocked.Increment(ref _next);
        var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting[id] = reply;
        try
        {
            await Send(id, method, parameters, session);
            return await reply.Task.WaitAsync(within ?? TimeSpan.FromSeconds(5));
        }
        finally { _waiting.TryRemove(id, out _); }
    }

    /// <summary>A request whose reply nobody waits for.</summary>
    internal Task Tell(string method, JsonObject parameters, string? session = null)
        => Send(Interlocked.Increment(ref _next), method, parameters, session);

    /// <summary>The page <paramref name="target"/>, attached to: a session of its own on this connection.</summary>
    internal async Task<session.@this> Attach(string target)
    {
        var reply = await Ask("Target.attachToTarget", new JsonObject { ["targetId"] = target, ["flatten"] = true });
        if (!reply.TryGetProperty("result", out var result) || !result.TryGetProperty("sessionId", out var id))
            throw new InvalidOperationException($"DevTools didn't attach to {target}: {Said(reply)}");
        return new session.@this(this, target, id.GetString()!);
    }

    /// <summary>The browser's pages now, as DevTools lists them (<c>Target.getTargets</c>): each one's id, url, title.</summary>
    internal async Task<IReadOnlyList<(string target, string url, string title)>> Pages()
    {
        var reply = await Ask("Target.getTargets", new JsonObject());
        if (!reply.TryGetProperty("result", out var result) || !result.TryGetProperty("targetInfos", out var infos)) return [];
        return infos.EnumerateArray()
            .Where(info => info.GetProperty("type").GetString() == "page")
            .Select(info => (info.GetProperty("targetId").GetString()!, info.GetProperty("url").GetString() ?? "", info.GetProperty("title").GetString() ?? ""))
            .ToList();
    }

    /// <summary>What a reply said went wrong, or the reply itself.</summary>
    internal static string Said(JsonElement reply)
        => reply.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message) ? message.GetString() ?? "" : reply.ToString();

    private async Task Send(int id, string method, JsonObject parameters, string? session)
    {
        var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters };
        if (session != null) message["sessionId"] = session;
        await _sending.WaitAsync();
        try
        {
            var written = await _pipe.Write(new global::app.data.@this("", (global::app.type.item.text.@this)message.ToJsonString()));
            if (!written.Success) throw new InvalidOperationException($"DevTools' pipe refused {method}: {written.Error?.Message}");
        }
        finally { _sending.Release(); }
    }

    // each message as it comes: a reply to the request waiting on its id, else an event for whoever listens. An event
    // is handed on, not awaited: the one who hears it may ask the browser, whose answer this loop reads.
    private async Task Listen()
    {
        try
        {
            while (true)
            {
                var said = await _pipe.Read();
                if (!said.Success) break;
                var text = (await said.Value())?.ToString();
                if (string.IsNullOrEmpty(text)) continue;
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var id) && id.TryGetInt32(out var number))
                {
                    if (_waiting.TryRemove(number, out var waiting)) waiting.TrySetResult(root.Clone());
                    continue;
                }
                if (!root.TryGetProperty("method", out var method) || Heard == null) continue;
                var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;
                var session = root.TryGetProperty("sessionId", out var s) ? s.GetString() : null;
                var name = method.GetString()!;
                // each listener in order, one event at a time — on its own queue, so the pipe is never held by it
                foreach (var hear in Heard.GetInvocationList().Cast<Func<string, JsonElement, string?, Task>>())
                    Queue(hear).Writer.TryWrite((name, parameters, session));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or ObjectDisposedException or InvalidOperationException) { }
        finally
        {
            // nothing more will answer: what waits is told so, not left to time out
            foreach (var (_, waiting) in _waiting) waiting.TrySetException(new IOException("DevTools' pipe ended"));
            foreach (var queue in _queues.Values) queue.Writer.TryComplete();
        }
    }

    // a listener's events, waiting for it: one queue each, drained in order by a task of its own; what it throws is
    // its own (the queue goes on with the next event)
    private readonly ConcurrentDictionary<Func<string, JsonElement, string?, Task>, System.Threading.Channels.Channel<(string, JsonElement, string?)>> _queues = new();

    private System.Threading.Channels.Channel<(string, JsonElement, string?)> Queue(Func<string, JsonElement, string?, Task> hear)
        => _queues.GetOrAdd(hear, listener =>
        {
            var queue = System.Threading.Channels.Channel.CreateUnbounded<(string, JsonElement, string?)>(new() { SingleReader = true });
            _ = Task.Run(async () =>
            {
                await foreach (var (method, parameters, session) in queue.Reader.ReadAllAsync())
                {
                    try { await listener(method, parameters, session); }
                    catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException)) { }
                }
            });
            return queue;
        });

    public override string ToString() => "DevTools over a pipe";
}
