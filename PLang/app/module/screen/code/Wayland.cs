using System.Text.Json;
using System.Text.Json.Nodes;
using app.error;
using FilePath = app.type.item.path.file.@this;
using Verb = app.type.item.permission.Verb;
using Text = app.type.item.text.@this;

namespace app.module.screen.code;

/// <summary>
/// PlangOS's screen (Linux): a display that programs draw onto — a Wayland compositor inside
/// plang (<see cref="wayland.Display"/>). Its frames go to the app's output (to the host that
/// shows them); the host's input comes back in through screen.send.
/// </summary>
public sealed class Wayland : IScreen
{
    public string Name { get; init; } = "wayland";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    private const string SocketName = "wayland-plang";
    /// <summary>The title bars' font, from the app (in its own folder: no permission to ask for — a
    /// prompt would take a line of the host's input). PlangOS links /.fonts to the system's DejaVu.
    /// None: title bars without text.</summary>
    private const string FontPath = "/.fonts/DejaVuSans.ttf";

    public async Task<data.@this<Screen>> Open(open action)
    {
        var context = action.Context;
        // the socket lives in the app's .run folder; programs find it through XDG_RUNTIME_DIR
        var folder = FilePath.Resolve("/.run", context);
        await folder.Mkdir(context);
        var socket = FilePath.Resolve("/.run/" + SocketName, context);
        await socket.Delete(recursive: false, context);   // a socket left by an earlier run; none there is NotFound, and fine

        var allowed = await socket.Authorize(Verb.Write, context);
        if (!allowed.Success) return data.@this<Screen>.From(allowed);

        var toOutput = (await action.ToOutput.Value())!.Value;
        var output = toOutput ? (context.Actor.Channel[global::app.channel.list.@this.Output] as global::app.channel.type.stream.@this)?.Stream : null;
        var fontFile = FilePath.Resolve(FontPath, context);
        var font = await (await fontFile.Exists(context)).ToBooleanAsync() ? await fontFile.Read(context) : null;
        var width = (int)(await action.Width.Value())!.ToDouble();
        var height = (int)(await action.Height.Value())!.ToDouble();
        // the display's notes, written one at a time, in order (it speaks from several threads)
        var notes = System.Threading.Channels.Channel.CreateUnbounded<string>();
        _ = Task.Run(async () =>
        {
            // the debug channel is there with --debug only
            await foreach (var note in notes.Reader.ReadAllAsync())
                if (context.App.Debug is { } debug) await debug.Write(note);
        });
        var display = new wayland.Display(new wayland.Size(width, height), "is", socket.Absolute, output,
            font == null ? null : (await font.Value())?.RawBytes, note => notes.Writer.TryWrite(note));

        var screen = new Screen
        {
            Title = (await action.Title.Value())?.ToString() ?? "", Width = width, Height = height,
            Display = display, Runtime = folder.Absolute, Socket = SocketName,
        };

        var onWindow = action.OnWindow == null ? null : await action.OnWindow.Value();
        // what happens to windows goes to OnWindow in order, and an element's click to what is bound on it
        // (on click on #window.bot) — but the display never waits for either: a goal running now (one at a time
        // per app) may itself wait for a window to be shown, and showing it is the display's next event
        var told = System.Threading.Channels.Channel.CreateUnbounded<System.Text.Json.Nodes.JsonObject>();
        _ = Task.Run(async () =>
        {
            await foreach (var e in told.Reader.ReadAllAsync())
            {
                if (e["ui"]?.GetValue<string>() == "click" && e["element"]?.GetValue<string>() is { } selector)
                {
                    if (screen.element.Held(selector) is { } element)
                        await global::app.module.on.code.Gate.Run(() => element.Clicked(Payload(e, context), context), context);
                }
                else if (onWindow != null)
                    await global::app.module.on.code.Gate.Call(onWindow, Payload(e, context), context);
            }
        });
        display.Told += e => { told.Writer.TryWrite(e); return Task.CompletedTask; };
        // what happens to the windows, when %screen.debug% asks (on under --debug): to the debug output with --debug,
        // else the error output (stderr) — never a goal (notes aren't errors; the screen's output is its frames)
        display.Watching = context.App.Debug != null;
        var watched = System.Threading.Channels.Channel.CreateUnbounded<string>();
        display.Noted = note => watched.Writer.TryWrite(note);
        _ = Task.Run(async () =>
        {
            await foreach (var note in watched.Reader.ReadAllAsync())
                if (context.App.Debug is { } debug) await debug.Write(note);
                else await context.App.actor.list.System.Channel[global::app.channel.list.@this.Error].WriteText(note);
        });
        display.Start();
        // the screen's output is the pipe to the host plang that started this one: the host is %!app.parent%, and
        // a call to one of its goals goes up beside the frames (its answer comes down the input: screen.listen)
        if (output != null) context.App.parent.Link = json => { display.Up(json); return Task.CompletedTask; };

        var opened = context.Ok<Screen>(screen);
        // the screen in play for the goals here: %!screen% — a bare #window.bot in a step is one of its elements
        context.Variable.Set("!screen", opened);
        return opened;
    }

    /// <summary>A window event as <c>%!data%</c>: a dict, so a goal can read its fields.</summary>
    private static data.@this Payload(JsonObject e, actor.context.@this context)
    {
        using var json = JsonDocument.Parse(e.ToJsonString());
        return new data.@this("!data", new global::app.type.item.serializer.json(context).Parse(json.RootElement.Clone()), context: context);
    }

    public async Task<data.@this> Send(send action)
    {
        var context = action.Context;
        if (await action.Screen.Value() is not { Display: { } display })
            return context.Error(new ActionError("The screen isn't open.", "ScreenNotOpen", 409));
        // the Data writes itself as text (a dict as its json): one line for the display
        using var line = new MemoryStream();
        await Text.Encode(line, action.Data, context, null, null, CancellationToken.None);
        display.Input(Originated(System.Text.Encoding.UTF8.GetString(line.ToArray()), action.Data.Context ?? context));
        return context.Ok();
    }

    // A window's address ({"window":"url","url":…}) knows where it came from: the Data's context — the
    // step that sent it, its goal and that goal's .pr — joins what the page said of itself ("origin")
    private static string Originated(string line, actor.context.@this context)
    {
        System.Text.Json.Nodes.JsonObject? e;
        try { e = System.Text.Json.Nodes.JsonNode.Parse(line) as System.Text.Json.Nodes.JsonObject; }
        catch (System.Text.Json.JsonException) { return line; }
        // "window" is a command's name here; a message that only mentions a window (its number) passes as it is
        if (!(e?["window"] is System.Text.Json.Nodes.JsonValue command && command.TryGetValue<string>(out var name) && name == "url")
            || context.CallStack is not { Step: { } step } stack) return line;
        if (e["url"] is not System.Text.Json.Nodes.JsonObject url)
            e["url"] = url = new System.Text.Json.Nodes.JsonObject { ["path"] = e["url"]?.DeepClone() };
        var origin = url["origin"] as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
        origin["pr"] = stack.Goal?.PrPath?.ToString();
        origin["goal"] = stack.Goal?.Name;
        origin["step"] = new System.Text.Json.Nodes.JsonObject { ["index"] = step.Index, ["text"] = step.Text };
        url["origin"] = origin;
        return e.ToJsonString();
    }

    public Task<data.@this> Draw(draw action)
        => Task.FromResult(action.Context.Error(new ActionError("PlangOS's screen makes frames; programs draw onto it (start browser … on %screen%).", "NotSupported", 400)));

    public async Task<data.@this> Close(close action)
    {
        if (await action.Screen.Value() is { Display: { } display }) display.Stop();
        return action.Context.Ok();
    }
}
