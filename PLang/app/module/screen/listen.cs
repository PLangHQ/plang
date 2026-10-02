namespace app.module.screen;

/// <summary>
/// The screen takes this app's input itself: each line that arrives (an event from the host:
/// <c>{"mouse":…}</c>, <c>{"key":…}</c>, …) goes straight to it — no goal per line, and nothing
/// waits behind a goal: a goal that waits for the person (a question on the screen) can't hold up
/// the click that answers it. Returns when the input ends (the host closed it). For PlangOS, whose
/// input is the pipe from host plang.
/// </summary>
[Action("listen", Cacheable = false)]
public partial class listen : IContext
{
    /// <summary>The screen, from <c>screen.open</c>.</summary>
    public partial data.@this<type.screen.@this> Screen { get; init; }

    public async Task<data.@this> Start() => await Screen.Value() is { } screen
        ? await screen.Listen(Context)
        : Context.Error(new global::app.error.ActionError("The screen isn't open.", "ScreenNotOpen", 409));
}
