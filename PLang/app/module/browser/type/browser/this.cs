using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using app.Attributes;
using FilePath = global::app.type.item.path.file.@this;
using Verb = global::app.type.item.permission.Verb;

namespace app.module.browser.type.browser;

/// <summary>
/// PLang <c>browser</c> value — a Chromium started with <c>browser.start</c>. Two kinds, each owning what it does:
/// <see cref="headless.@this"/> renders one page off-screen and gives each new frame to OnFrame, its input coming in
/// through <c>browser.send</c>; <see cref="screen.@this"/> runs as a normal browser drawing onto PlangOS's display, its
/// pages windows (the first the desktop, more with <c>window.open</c>), the screen giving it the pointer and keyboard.
/// Which it is is its kind: <c>%browser!type.kind%</c> (<c>headless</c>, <c>screen</c>). <c>browser.stop</c> ends either.
/// </summary>
[PlangType("browser"), Kinds]
public abstract partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly string _url;
    private readonly int _width, _height;

    private protected @this(string url, int width, int height, System.Diagnostics.Process os)
    {
        _url = url;
        _width = width;
        _height = height;
        Os = os;
    }

    /// <summary>Its kind — headless, screen.</summary>
    private protected abstract string Variant { get; }

    protected internal override global::app.type.@this Type => new("browser", typeof(@this), Variant);

    public override bool IsLeaf => false;

    /// <summary>The page it opened first.</summary>
    [LlmBuilder, Out] public global::app.type.item.text.@this Url => _url;

    /// <summary>Its width in pixels.</summary>
    [LlmBuilder, Out] public global::app.type.item.number.@this Width => _width;

    /// <summary>Its height in pixels.</summary>
    [LlmBuilder, Out] public global::app.type.item.number.@this Height => _height;

    /// <summary>True until the browser exits.</summary>
    [LlmBuilder, Out] public global::app.type.item.@bool.@this Running => !Os.HasExited;

    internal string Address => _url;
    private protected int PixelWidth => _width;
    private protected int PixelHeight => _height;

    /// <summary>Its Chromium.</summary>
    internal System.Diagnostics.Process Os { get; }

    /// <summary>Gives it one line from the person — an input, a navigation: <c>browser.send</c>.</summary>
    internal abstract Task<global::app.data.@this> Send(global::app.data.@this line, global::app.actor.context.@this context);

    // ---- its end ---------------------------------------------------------------------------------

    // Chromium's last words on its error output — what it said before it stopped, if it stops
    private readonly Queue<string> _said = new();

    /// <summary>A line Chromium wrote on its error output: the last 40 are kept.</summary>
    internal void Heard(string line)
    {
        lock (_said)
        {
            _said.Enqueue(line);
            while (_said.Count > 40) _said.Dequeue();
        }
    }

    /// <summary>True once plang stops it (browser.stop): its exit is then no failure.</summary>
    private bool _stopping;

    /// <summary>Says what went wrong away from any step: written to its app's error channel. (An item holds no
    /// context; this is where the one it was started in reports.)</summary>
    internal Func<global::app.error.Error, Task>? Report { get; private protected init; }

    /// <summary>Its Chromium exited: unless plang stopped it, that is a failure nothing else would see. Said on the
    /// error output (the host's console) and the app's error channel, with what Chromium said last.</summary>
    private protected async Task Exited()
    {
        if (_stopping) return;
        string said;
        lock (_said) said = string.Join("\n", _said);
        var message = $"Chromium stopped by itself (exit code {Os.ExitCode}){Lost}."
            + (said.Length > 0 ? "\nWhat it said last:\n" + said : "");
        Console.Error.WriteLine(message);
        if (Report != null) await Report(new global::app.error.ServiceError(message, "BrowserStopped", 500));
    }

    /// <summary>What its stopping takes with it, said when it stops by itself.</summary>
    private protected virtual string Lost => "";

    /// <summary>Its Chromium's exit is watched from now on: stopping by itself is said.</summary>
    private protected void Watched()
    {
        Os.EnableRaisingEvents = true;
        Os.Exited += (_, _) => _ = Exited();
        if (Os.HasExited) _ = Exited();
    }

    /// <summary>Ends it: asked to close, then ended if it doesn't within a second — <c>browser.stop</c>.</summary>
    internal async Task Stop()
    {
        _stopping = true;
        try { if (Speaks is { State: WebSocketState.Open } speaks) await Cdp("Browser.close", new JsonObject(), speaks); }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException) { }
        if (!Os.HasExited && !Os.WaitForExit(1000)) Os.Kill(entireProcessTree: true);
    }

    // ---- DevTools (the Chrome DevTools Protocol, on a WebSocket bound to 127.0.0.1) ----------------

    /// <summary>The connection that speaks for it as a whole.</summary>
    private protected abstract ClientWebSocket? Speaks { get; }

    private readonly SemaphoreSlim _sending = new(1, 1);
    private int _messageId;
    /// <summary>DevTools requests waiting for their reply, by id.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();

    /// <summary>A request, its reply not waited for; to <see cref="Speaks"/> unless a socket is named.</summary>
    private protected Task Cdp(string method, JsonObject parameters, ClientWebSocket? socket = null)
        => Post(Interlocked.Increment(ref _messageId), method, parameters, socket);

    /// <summary>A request whose reply is wanted: the raw reply JSON, or a timeout after 2 s.</summary>
    private protected async Task<string> Ask(string method, JsonObject parameters, ClientWebSocket? socket = null)
    {
        var id = Interlocked.Increment(ref _messageId);
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = reply;
        try
        {
            await Post(id, method, parameters, socket);
            return await reply.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { _pending.TryRemove(id, out _); }
    }

    /// <summary>A reply arrived: the request waiting on <paramref name="id"/> has it. False when none waits.</summary>
    private protected bool Answered(int id, string reply)
    {
        if (!_pending.TryRemove(id, out var waiting)) return false;
        waiting.TrySetResult(reply);
        return true;
    }

    private async Task Post(int id, string method, JsonObject parameters, ClientWebSocket? socket)
    {
        var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters };
        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
        await _sending.WaitAsync();
        try { await (socket ?? Speaks!).SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { _sending.Release(); }
    }

    // ---- starting Chromium -----------------------------------------------------------------------

    [GeneratedRegex(@"DevTools listening on ws://127\.0\.0\.1:(\d+)/")]
    private static partial Regex Listening();

    private protected static readonly HttpClient DevTools = new() { Timeout = TimeSpan.FromSeconds(3) };

    /// <summary>The app's browser profile, as a plang path (from the app's root).</summary>
    private protected const string Profiled = "/.browser";

    /// <summary>The profile folder Chromium keeps its data in — through the path gate (it writes there), before its
    /// <c>.Absolute</c> goes to Chromium; a refusal is the answer.</summary>
    private protected static async Task<(FilePath? folder, global::app.data.@this? refused)> Profile(global::app.actor.context.@this context)
    {
        var folder = FilePath.Resolve(Profiled, context);
        var allowed = await folder.Authorize(Verb.write, context);
        return allowed.Success && !allowed.Exits ? (folder, null) : (null, allowed);
    }

    /// <summary>The DevTools port Chromium says it took; what it says after that goes to <paramref name="told"/>, or
    /// nowhere.</summary>
    private protected static async Task<int?> PortOf(StreamReader stderr, TimeSpan within, Func<string, Task>? told = null)
    {
        using var cts = new CancellationTokenSource(within);
        try
        {
            while (await stderr.ReadLineAsync(cts.Token) is { } line)
            {
                var match = Listening().Match(line);
                if (!match.Success) continue;
                _ = Drain(stderr, told);
                return int.Parse(match.Groups[1].Value);
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    /// <summary>The DevTools address of its first page.</summary>
    private protected static async Task<string?> PageOf(int port)
    {
        for (var i = 0; i < 40; i++)
        {
            try
            {
                using var doc = JsonDocument.Parse(await DevTools.GetStringAsync($"http://127.0.0.1:{port}/json/list"));
                foreach (var target in doc.RootElement.EnumerateArray())
                    if (target.GetProperty("type").GetString() == "page")
                        return target.GetProperty("webSocketDebuggerUrl").GetString();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { }
            await Task.Delay(250);
        }
        return null;
    }

    private protected static async Task Drain(StreamReader reader, Func<string, Task>? told = null)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
                if (told != null) await told(line);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }

    private protected static global::app.data.@this<@this> Fail(global::app.actor.context.@this context, string message, string key, int status)
        => global::app.data.@this<@this>.From(context.Error(new global::app.error.ActionError(message, key, status)));

    public override string ToString() => $"browser {_url} ({_width}x{_height}{(Os.HasExited ? ", exited" : "")})";
}
