using System.Runtime.CompilerServices;
using Call = app.goal.step.action.@this;

namespace app.module.on.code;

/// <summary>
/// Runs goal calls that arrive from outside the step loop — a line from a running program, a frame
/// from a browser, a click on a screen — one at a time per app. They share the actor's variables
/// (<c>%!data%</c> above all), so two at once would read each other's values.
/// </summary>
public static class Gate
{
    private static readonly ConditionalWeakTable<global::app.@this, SemaphoreSlim> Gates = new();

    /// <summary>Binds <paramref name="payload"/> as <c>%!data%</c> and runs <paramref name="held"/>, after any
    /// call already running for the same app. A failing call is written to the error channel; the source
    /// keeps going.</summary>
    public static async Task Call(Call held, data.@this payload, actor.context.@this context)
    {
        var gate = Gates.GetValue(context.App, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            payload.Name = "!data";
            await context.Variable.Set("!data", payload);
            var result = await held.Start(context);
            if (!result.Success)
                await context.App.actor.list.System.Channel[global::app.channel.list.@this.Error].WriteText(result.Error?.Message ?? "");
        }
        finally { gate.Release(); }
    }
}
