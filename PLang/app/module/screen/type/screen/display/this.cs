using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using app.error;
using FilePath = global::app.type.item.path.file.@this;
using Verb = global::app.type.item.permission.Verb;
using Text = global::app.type.item.text.@this;
using Wayland = global::app.module.screen.type.screen.display.code.Display;

namespace app.module.screen.type.screen.display;

/// <summary>
/// A screen that is PlangOS's display (Linux): programs draw onto it — a Wayland compositor inside plang. Its frames go
/// to the app's output (to the host that shows them); the host's input comes back in through screen.send, or the screen
/// takes the app's input itself (screen.listen). What happens to its windows goes to OnWindow, an element's click to
/// what is bound on it.
/// </summary>
public sealed class @this : screen.@this
{
    private const string SocketName = "wayland-plang";
    /// <summary>The title bars' font, from the app (in its own folder: no permission to ask for — a
    /// prompt would take a line of the host's input). PlangOS links /.fonts to the system's DejaVu.
    /// None: title bars without text.</summary>
    private const string FontPath = "/.fonts/DejaVuSans.ttf";

    private @this(string title, int width, int height, Wayland display, string runtime) : base(title, width, height)
    {
        Wayland = display;
        Runtime = runtime;
    }

    /// <summary>The compositor programs draw onto.</summary>
    internal Wayland Wayland { get; }

    /// <summary>Where programs find the display: its socket's folder (XDG_RUNTIME_DIR) and name (WAYLAND_DISPLAY).</summary>
    internal string Runtime { get; }
    internal string Socket => SocketName;

    /// <summary>Opens the display <paramref name="width"/>×<paramref name="height"/>, its socket in the app's .run
    /// folder; its frames to the app's output when <paramref name="toOutput"/>. It becomes <c>%!screen%</c>, the screen
    /// in play for the goals here.</summary>
    internal static async Task<global::app.data.@this<screen.@this>> Start(string title, int width, int height, bool toOutput,
        global::app.goal.step.action.@this? onWindow, global::app.actor.context.@this context)
    {
        // the socket lives in the app's .run folder; programs find it through XDG_RUNTIME_DIR
        var folder = FilePath.Resolve("/.run", context);
        await folder.Mkdir(context);
        var socket = FilePath.Resolve("/.run/" + SocketName, context);
        await socket.Delete(recursive: false, context);   // a socket left by an earlier run; none there is NotFound, and fine

        var allowed = await socket.Authorize(Verb.write, context);
        if (!allowed.Success) return global::app.data.@this<screen.@this>.From(allowed);

        var output = toOutput ? (context.Actor.Channel[global::app.channel.list.@this.Output] as global::app.channel.type.stream.@this)?.Stream : null;
        var fontFile = FilePath.Resolve(FontPath, context);
        var font = await (await fontFile.Exists(context)).ToBooleanAsync() ? await fontFile.Read(context) : null;
        // the display's notes, written one at a time, in order (it speaks from several threads)
        var notes = System.Threading.Channels.Channel.CreateUnbounded<string>();
        _ = Task.Run(async () =>
        {
            // the debug channel is there with --debug only
            await foreach (var note in notes.Reader.ReadAllAsync())
                if (context.App.Debug is { } debug) await debug.Write(note);
        });
        var display = new Wayland(new code.Size(width, height), "is", socket.Absolute, output,
            font == null ? null : (await font.Value())?.RawBytes, note => notes.Writer.TryWrite(note));
        var screen = new @this(title, width, height, display, folder.Absolute);
        screen.Told(onWindow, context);
        screen.Watched(context);
        display.Start();
        // the screen's output is the pipe to the host plang that started this one: the host is %!app.parent%, and
        // a call to one of its goals goes up beside the frames (its answer comes down the input: screen.listen)
        if (output != null) context.App.parent.Link = json => { display.Up(json); return Task.CompletedTask; };

        var opened = context.Ok<screen.@this>(screen);
        // the screen in play for the goals here: %!screen% — a bare #window.bot in a step is one of its elements
        await context.Variable.Set("!screen", opened);
        return opened;
    }

    // what happens to windows goes to OnWindow in order, and an element's click to what is bound on it
    // (on click on #window.bot) — but the display never waits for either: a goal running now (one at a time
    // per app) may itself wait for a window to be shown, and showing it is the display's next event
    private void Told(global::app.goal.step.action.@this? onWindow, global::app.actor.context.@this context)
    {
        var told = System.Threading.Channels.Channel.CreateUnbounded<JsonObject>();
        _ = Task.Run(async () =>
        {
            await foreach (var e in told.Reader.ReadAllAsync())
            {
                // one event that fails is said, and the next still goes: a failure here would otherwise end this
                // loop without a word, and with it every click and window after it
                try
                {
                    if (e["ui"]?.GetValue<string>() == "click" && e["element"]?.GetValue<string>() is { } selector)
                    {
                        if (element.Held(selector) is { } clicked)
                            await global::app.module.on.code.Gate.Run(() => clicked.Clicked(Payload(e, context), context), context);
                    }
                    else if (onWindow != null)
                        await global::app.module.on.code.Gate.Call(onWindow, Payload(e, context), context);
                }
                catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
                {
                    await context.Actor.Channel.Report(context.Error(new ServiceError(
                        $"The screen could not hand on {e.ToJsonString()}: {ex.Message}", "ScreenEventFailed") { Exception = ex }));
                }
            }
        });
        // and to what draws onto the display (a browser pairing its windows with their pages), in order, on a pump of
        // its own: it waits on its program (DevTools), and must never wait behind a goal, nor a goal behind it
        var followed = System.Threading.Channels.Channel.CreateUnbounded<JsonObject>();
        _ = Task.Run(async () =>
        {
            await foreach (var e in followed.Reader.ReadAllAsync())
            {
                if (Followed is not { } follow) continue;
                foreach (var one in follow.GetInvocationList().Cast<Func<JsonObject, global::app.actor.context.@this, Task>>())
                {
                    try { await one(e, context); }
                    catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
                    {
                        await context.Actor.Channel.Report(context.Error(new ServiceError(
                            $"The screen could not hand on {e.ToJsonString()}: {ex.Message}", "ScreenEventFailed") { Exception = ex }));
                    }
                }
            }
        });
        Wayland.Told += e =>
        {
            told.Writer.TryWrite(e);
            followed.Writer.TryWrite(e);
            return Task.CompletedTask;
        };
    }

    /// <summary>What happens to the display's windows, for what draws onto it (a browser pairs its windows with their
    /// pages): each event in order, with the context the display was opened in.</summary>
    internal event Func<JsonObject, global::app.actor.context.@this, Task>? Followed;

    // what happens to the windows, when %screen.verbose% asks (on under --debug): to the debug output with --debug,
    // else the error output (stderr) — never a goal (notes aren't errors; the screen's output is its frames)
    private void Watched(global::app.actor.context.@this context)
    {
        Wayland.Watching = context.App.Debug != null;
        var watched = System.Threading.Channels.Channel.CreateUnbounded<string>();
        Wayland.Noted = note => watched.Writer.TryWrite(note);
        _ = Task.Run(async () =>
        {
            await foreach (var note in watched.Reader.ReadAllAsync())
                if (context.App.Debug is { } debug) await debug.Write(note);
                else await context.App.actor.list.System.Channel[global::app.channel.list.@this.Error].WriteText(note);
        });
    }

    /// <summary>A window event as <c>%!data%</c>: a dict, so a goal can read its fields.</summary>
    private static global::app.data.@this Payload(JsonObject e, global::app.actor.context.@this context)
    {
        using var json = JsonDocument.Parse(e.ToJsonString());
        return new global::app.data.@this("!data", new global::app.type.item.serializer.json(context).Parse(json.RootElement.Clone()), context: context);
    }

    private protected override bool IsClosed => false;

    private protected override bool Noting
    {
        get => Wayland.Watching;
        set => Wayland.Watching = value;
    }

    internal override void Bind(string selector)
        => Wayland.Input(new JsonObject { ["ui"] = "bind", ["element"] = selector }.ToJsonString());

    internal override Task<global::app.data.@this> Draw(global::app.data.@this frame, global::app.actor.context.@this context)
        => Task.FromResult(context.Error(new ActionError("PlangOS's screen makes frames; programs draw onto it (start browser … on %screen%).", "NotSupported", 400)));

    internal override async Task<global::app.data.@this> Send(global::app.data.@this line, global::app.actor.context.@this context)
    {
        // the Data writes itself as text (a dict as its json): one line for the display
        using var written = new MemoryStream();
        await Text.Encode(written, line, context, null, null, CancellationToken.None);
        Wayland.Input(Originated(Encoding.UTF8.GetString(written.ToArray()), line.Context ?? context));
        return context.Ok();
    }

    // A window's address ({"window":"url","url":…}) knows where it came from: the Data's context — the
    // step that sent it, its goal and that goal's .pr — joins what the page said of itself ("origin")
    private static string Originated(string line, global::app.actor.context.@this context)
    {
        JsonObject? e;
        try { e = JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { return line; }
        // "window" is a command's name here; a message that only mentions a window (its number) passes as it is
        if (!(e?["window"] is JsonValue command && command.TryGetValue<string>(out var name) && name == "url")
            || context.call is not { Step: { } step } stack) return line;
        if (e["url"] is not JsonObject url)
            e["url"] = url = new JsonObject { ["path"] = e["url"]?.DeepClone() };
        var origin = url["origin"] as JsonObject ?? new JsonObject();
        origin["pr"] = stack.Goal?.PrPath?.ToString();
        origin["goal"] = stack.Goal?.Name;
        origin["step"] = new JsonObject { ["index"] = step.Index, ["text"] = step.Text };
        url["origin"] = origin;
        return e.ToJsonString();
    }

    /// <summary>
    /// The screen takes this app's input itself: each line that arrives (an event from the host:
    /// <c>{"mouse":…}</c>, <c>{"key":…}</c>, …) goes straight to it — no goal per line, and nothing
    /// waits behind a goal: a goal that waits for the person (a question on the screen) can't hold up
    /// the click that answers it. Returns when the input ends (the host closed it). The input is read through a handle
    /// of its own (the process's stdin, opened anew): the app's input channel may be something else meanwhile (PlangOS
    /// asks the person through a goal) without taking the host's events with it.
    /// </summary>
    internal override async Task<global::app.data.@this> Listen(global::app.actor.context.@this context)
    {
        var lines = 0;
        using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false), false, 1 << 16);
        while (await reader.ReadLineAsync() is { } line)
        {
            lines++;
            // the host answering a call to one of its goals ({"reply": …}) goes to the call waiting on it
            if (line.StartsWith("{\"reply\"", StringComparison.Ordinal) && Member(line, "reply") is { } reply)
            {
                context.App.parent.Answered(reply);
                continue;
            }
            // the host calling one of this shell's goals ({"call": {id, goal, parameters}} — %container.goal["Question"]%
            // there): it runs here, one at a time with the other callbacks, and its answer goes up beside the frames
            if (line.StartsWith("{\"call\"", StringComparison.Ordinal) && Member(line, "call") is { } call)
            {
                _ = Called(call, context);
                continue;
            }
            // the mouse, a key, typed text, back/forward/reload, what the host copied: read as values, each handing
            // itself to the display. A line that claims to be one but isn't is said, never dropped quietly.
            global::app.type.item.@this? value;
            try { value = Value(line, context); }
            catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
            {
                await context.Actor.Channel.Report(context.Error(new ActionError(
                    $"The host sent an input the screen can't read: {ex.Message} — {(line.Length > 200 ? line[..200] + "…" : line)}", "InputInvalid", 400)));
                continue;
            }
            if (value != null) Wayland.Take(value);
            else Wayland.Input(line);
        }
        return context.Ok<global::app.type.item.number.@this>(lines);
    }


    /// <summary>Runs the shell's goal the host called, its parameters bound by name, and answers it:
    /// <c>{"reply": {id, result}}</c>, or <c>{"reply": {id, error}}</c>. Only the shell's own goals — a bare name,
    /// as seen from the goal that listens (Screen.goal and its folder) — never a path elsewhere.</summary>
    private async Task Called(JsonElement call, global::app.actor.context.@this context)
    {
        var id = call.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        var name = call.TryGetProperty("goal", out var g) ? g.GetString() ?? "" : "";
        async Task Answer(JsonObject reply)
        {
            reply["id"] = id;
            Wayland.Up(new JsonObject { ["reply"] = reply }.ToJsonString());
            await Task.CompletedTask;
        }
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\'))
        {
            await Answer(new() { ["error"] = $"PlangOS runs only its shell's own goals, by name: not '{name}'" });
            return;
        }
        await global::app.module.on.code.Gate.Run(async () =>
        {
            var found = await context.App.goal.list.Find(name, context.call.Goal);
            if (!found.Success || await found.Value() is not { } goal)
            {
                await Answer(new() { ["error"] = $"PlangOS's shell has no goal {name}" });
                return;
            }
            var bound = new List<global::app.data.@this>();
            if (call.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
            {
                using var named = parameters.EnumerateObject();
                foreach (var p in named)
                    bound.Add(new global::app.data.@this(p.Name, new global::app.type.item.serializer.json(context).Parse(p.Value.Clone()), context: context));
            }
            // its parameters are its goal frame's own variables
            var ran = await goal.Start(context, bound);
            if (!ran.Success)
            {
                await Answer(new() { ["error"] = ran.Error?.Message ?? "failed" });
                return;
            }
            using var written = new MemoryStream();
            var encoded = await context.App.type.list.Mime("application/json").Encode(written, ran, context);
            if (!encoded.Success) { await Answer(new() { ["error"] = encoded.Error?.Message ?? "its answer couldn't be written" }); return; }
            var text = Encoding.UTF8.GetString(written.ToArray());
            JsonNode? result;
            try { result = JsonNode.Parse(text); }
            catch (JsonException) { result = JsonValue.Create(text); }
            await Answer(new() { ["result"] = result });
        }, context);
    }

    private static JsonElement? Member(string line, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty(name, out var member) ? member.Clone() : null;
        }
        catch (JsonException) { return null; }
    }

    internal override void Close() => Wayland.Stop();
}
