using System.Text;

namespace app.module.screen;

/// <summary>
/// The screen takes this app's input itself: each line that arrives (an event from the host:
/// <c>{"mouse":…}</c>, <c>{"key":…}</c>, …) goes straight to it — no goal per line, and nothing
/// waits behind a goal: a goal that waits for the person (a question on the screen) can't hold up
/// the click that answers it. Returns when the input ends (the host closed it). For PlangOS, whose
/// input is the pipe from host plang. The input is read through a handle of its own (the process's
/// stdin, opened anew): the app's input channel may be something else meanwhile (PlangOS asks the
/// person through a goal) without taking the host's events with it.
/// </summary>
[Action("listen", Cacheable = false)]
public partial class listen : IContext
{
    /// <summary>The screen, from <c>screen.open</c>.</summary>
    public partial data.@this<Screen> Screen { get; init; }

    public async Task<data.@this> Start()
    {
        var screen = await Screen.Value();
        if (screen?.Display is not { } display)
            return Context.Error(new global::app.error.ActionError("This screen takes no input (it isn't PlangOS's display).", "NotSupported", 400));
        var lines = 0;
        using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false), false, 1 << 16);
        while (await reader.ReadLineAsync() is { } line)
        {
            lines++;
            display.Input(line);
        }
        return Context.Ok<global::app.type.item.number.@this>(lines);
    }
}
