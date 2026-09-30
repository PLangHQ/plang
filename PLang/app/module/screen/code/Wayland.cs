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
        await socket.Delete(recursive: false, ignoreIfNotFound: true, context);
        var allowed = await socket.Authorize(Verb.Write, context);
        if (!allowed.Success) return data.@this<Screen>.From(allowed);

        var toOutput = (await action.ToOutput.Value())!.Value;
        var output = toOutput ? (context.Actor.Channel[global::app.channel.list.@this.Output] as global::app.channel.type.stream.@this)?.Stream : null;
        var fontFile = FilePath.Resolve(FontPath, context);
        var font = await (await fontFile.ExistsAsync(context)).ToBooleanAsync() ? await fontFile.Read(context) : null;
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

        var onWindow = action.OnWindow == null ? null : await action.OnWindow.Value();
        if (onWindow != null)
            display.Told += e => global::app.module.on.code.Gate.Call(onWindow, Payload(e, context), context);
        display.Start();

        var screen = new Screen
        {
            Title = (await action.Title.Value())?.ToString() ?? "", Width = width, Height = height,
            Display = display, Runtime = folder.Absolute, Socket = SocketName,
        };
        return context.Ok<Screen>(screen);
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
        display.Input(System.Text.Encoding.UTF8.GetString(line.ToArray()));
        return context.Ok();
    }

    public Task<data.@this> Draw(draw action)
        => Task.FromResult(action.Context.Error(new ActionError("PlangOS's screen makes frames; programs draw onto it (start browser … on %screen%).", "NotSupported", 400)));

    public async Task<data.@this> Close(close action)
    {
        if (await action.Screen.Value() is { Display: { } display }) display.Stop();
        return action.Context.Ok();
    }
}
