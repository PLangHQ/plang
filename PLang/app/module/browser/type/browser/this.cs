using System.Text.Json.Nodes;
using app.Attributes;
using Program = global::app.module.terminal.Process;

namespace app.module.browser.type.browser;

/// <summary>
/// PLang <c>browser</c> value — a Chromium started with <c>browser.start</c>. Two kinds, each owning what it does:
/// <see cref="headless.@this"/> renders one page off-screen and gives each new frame to OnFrame, its input coming in
/// through <c>browser.send</c>; <see cref="screen.@this"/> runs as a normal browser drawing onto PlangOS's display, its
/// pages windows (the first the desktop, more with <c>window.open</c>), the screen giving it the pointer and keyboard.
/// Which it is is its kind: <c>%browser!type.kind%</c> (<c>headless</c>, <c>screen</c>). <c>browser.stop</c> ends either.
///
/// <para>Chromium is a program a goal that ships with plang starts (<c>/system/browser/</c>, through terminal: trusted
/// by its origin, decision 579 — it names the program and every flag itself), and DevTools speaks over that program's
/// pipe (<see cref="cdp.@this"/>). What the caller gives — the page, the size — goes over the pipe, never on Chromium's
/// command line.</para>
/// </summary>
[PlangType("browser"), Kinds]
public abstract partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly string _url;
    private readonly int _width, _height;
    private bool _stopping;

    private protected @this(string url, int width, int height, Program program, cdp.@this cdp)
    {
        _url = url;
        _width = width;
        _height = height;
        Program = program;
        Cdp = cdp;
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
    [LlmBuilder, Out] public global::app.type.item.@bool.@this Running => Program.Os is { HasExited: false };

    /// <summary>The Chromium program, as terminal runs it.</summary>
    internal Program Program { get; }

    /// <summary>DevTools, over the program's pipe.</summary>
    internal cdp.@this Cdp { get; }

    /// <summary>Gives it what the person did — the mouse, a key, typed text, back/forward/reload: <c>browser.send</c>.</summary>
    internal abstract Task<global::app.data.@this> Send(global::app.type.item.input.@this input, global::app.actor.context.@this context);

    // ---- starting -----------------------------------------------------------------------------------

    /// <summary>Chromium, as the goal of plang's own that starts it runs it (<c>/system/browser/&lt;goal&gt;</c>): the
    /// program, its pipe a channel; or why not.</summary>
    private protected static async Task<(Program? program, global::app.data.@this? failed)> Started(string goal, global::app.actor.context.@this context)
    {
        var found = await context.App.goal.list.Find("/system/browser/" + goal, context.call.Goal);
        if (!found.Success || await found.Value() is not { } starts) return (null, found.Success ? Fail(context, $"No /system/browser/{goal} goal", "BrowserNotFound", 404) : found);
        var ran = await starts.Start(context, []);
        if (!ran.Success) return (null, ran);
        if (await ran.Value() is not Program { pipe: not null } program)
            return (null, Fail(context, $"/system/browser/{goal} started no Chromium with a pipe", "BrowserStartFailed", 500));
        return (program, null);
    }

    /// <summary>A <c>file://</c> page is read as the caller (finding 1 of 579: a browser started trusted must not read
    /// for a caller what the caller couldn't read itself); a website is the browser's normal job. The refusal, or null.</summary>
    internal static async Task<global::app.data.@this?> Readable(string url, global::app.actor.context.@this context)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || !address.IsFile) return null;
        var allowed = await global::app.type.item.path.file.@this.Resolve(url, context).Authorize(global::app.type.item.permission.Verb.read, context);
        return allowed.Success && !allowed.Exits ? null : allowed;
    }

    private protected static global::app.data.@this<@this> Fail(global::app.actor.context.@this context, string message, string key, int status)
        => global::app.data.@this<@this>.From(context.Error(new global::app.error.ActionError(message, key, status)));

    // ---- its end ----------------------------------------------------------------------------------

    /// <summary>Says what went wrong away from any step: written to its app's error channel. (An item holds no
    /// context; this is where the one it was started in reports.)</summary>
    internal Func<global::app.error.Error, Task>? Report { get; private protected init; }

    /// <summary>What its stopping takes with it, said when it stops by itself.</summary>
    private protected virtual string Lost => "";

    /// <summary>Its Chromium's exit is watched from now on: stopping by itself — unless plang stopped it — is a failure
    /// nothing else would see, said on the app's error channel with what Chromium said last (its program's stderr).</summary>
    private protected void Watched() => _ = Task.Run(async () =>
    {
        await Program.Os!.WaitForExitAsync();
        if (_stopping || Report == null) return;
        var said = Program.Said;
        await Report(new global::app.error.ServiceError(
            $"Chromium stopped by itself (exit code {Program.Os.ExitCode}){Lost}." + (said.Length > 0 ? "\nWhat it said last:\n" + said : ""),
            "BrowserStopped", 500));
    });

    /// <summary>Ends it: asked to close, then ended if it doesn't within a second — <c>browser.stop</c>.</summary>
    internal async Task Stop()
    {
        _stopping = true;
        try { await Cdp.Tell("Browser.close", new JsonObject()); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException) { }
        if (Program.Os is { HasExited: false } os)
        {
            try { await os.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1)); }
            catch (TimeoutException) { os.Kill(); }
        }
    }

    public override string ToString() => $"browser {_url} ({_width}x{_height}{(Running.Value ? "" : ", exited")})";
}
