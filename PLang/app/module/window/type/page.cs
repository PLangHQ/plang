using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace app.module.window;

/// <summary>
/// The page a window shows, over a DevTools connection of its own. It goes where it is sent,
/// evaluates, and gives a message event. When it is one of the app's own pages (a file under the
/// app's folder), it also gets <c>plang(text)</c>: what it says is heard with its window's id added
/// (<c>"from"</c>). Every call is checked: a page that has since gone somewhere else (a website) is
/// not heard.
/// </summary>
internal sealed class Page(string target, int port, string root)
{
    private readonly ClientWebSocket socket = new();
    private readonly SemaphoreSlim sending = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> waiting = new();
    private int next;

    /// <summary>DevTools' id for this page.</summary>
    internal string Target { get; } = target;

    /// <summary>Connects. Given <paramref name="hear"/>, the page gets <c>plang(text)</c> and what it
    /// says goes there, <c>"from"</c> <paramref name="window"/>.</summary>
    internal async Task Open(long window, Func<string, Task>? hear)
    {
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/devtools/page/{Target}"), CancellationToken.None);
        _ = Task.Run(() => Listen(window, hear));
        if (hear == null) return;
        await Ask("Runtime.enable", new JsonObject());
        await Ask("Runtime.addBinding", new JsonObject { ["name"] = "plang" });
    }

    /// <summary>Goes to <paramref name="url"/>.</summary>
    internal Task Navigate(string url) => Ask("Page.navigate", new JsonObject { ["url"] = url });

    /// <summary>A message to the page: a <c>message</c> event, origin <c>"plang"</c>.</summary>
    internal Task Post(string text) => Ask("Runtime.evaluate", new JsonObject
    {
        ["expression"] = "window.dispatchEvent(new MessageEvent('message',{data:" + JsonSerializer.Serialize(text) + ",origin:'plang'}))",
    });

    /// <summary>Runs <paramref name="expression"/> in the page, a promise awaited; DevTools' answer.</summary>
    internal Task<JsonElement> Evaluate(string expression) => Ask("Runtime.evaluate", new JsonObject
    {
        ["expression"] = expression, ["awaitPromise"] = true, ["returnByValue"] = true,
    });

    /// <summary>The page closes (and the window it is in).</summary>
    internal Task Shut() => Ask("Page.close", new JsonObject());

    /// <summary>The connection goes.</summary>
    internal void Close()
    {
        try { socket.Abort(); } catch { }
    }

    private async Task Listen(long window, Func<string, Task>? hear)
    {
        var buffer = new byte[1 << 16];
        var message = new MemoryStream();
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var r = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, r.Count);
                if (!r.EndOfMessage) continue;
                using var doc = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                message.SetLength(0);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var id) && waiting.TryRemove(id.GetInt32(), out var answer))
                {
                    answer.TrySetResult(root.Clone());
                    continue;
                }
                if (hear == null || !root.TryGetProperty("method", out var m) || m.GetString() != "Runtime.bindingCalled") continue;
                var p = root.GetProperty("params");
                var said = p.GetProperty("payload").GetString() ?? "";
                var context = p.GetProperty("executionContextId").GetInt32();
                // not awaited: the goal may talk back here, whose answer this loop reads
                _ = Task.Run(async () =>
                {
                    if (await Ours(context)) await hear(From(window, said));
                });
            }
        }
        catch (Exception ex) when (ex is WebSocketException or JsonException or ObjectDisposedException) { }
    }

    /// <summary>The page that called is still one of the app's own (not a website the window went to).</summary>
    private async Task<bool> Ours(int context)
    {
        try
        {
            var reply = await Ask("Runtime.evaluate", new JsonObject { ["expression"] = "location.href", ["contextId"] = context, ["returnByValue"] = true });
            return reply.TryGetProperty("result", out var r) && r.TryGetProperty("result", out var v)
                && v.TryGetProperty("value", out var href) && href.ValueKind == JsonValueKind.String
                && href.GetString()!.StartsWith(root, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is TimeoutException or WebSocketException or KeyNotFoundException) { return false; }
    }

    /// <summary>What the page said, with its window's id: a json object gets <c>"from"</c>; anything else stays as it is.</summary>
    private static string From(long window, string said)
    {
        try
        {
            if (JsonNode.Parse(said) is JsonObject o)
            {
                o["from"] = window;
                return o.ToJsonString();
            }
        }
        catch (JsonException) { }
        return said;
    }

    private async Task<JsonElement> Ask(string method, JsonObject parameters)
    {
        var id = Interlocked.Increment(ref next);
        var answer = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting[id] = answer;
        var bytes = Encoding.UTF8.GetBytes(new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters }.ToJsonString());
        await sending.WaitAsync();
        try { await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { sending.Release(); }
        try { return await answer.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { waiting.TryRemove(id, out _); }
    }
}
