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
    /// call already running for the same app. A failing call is written to the error channel — one that
    /// threw too: its sources start it and don't wait (a browser's message, a program's line), so nothing
    /// else would ever see the exception. The source keeps going.</summary>
    public static async Task Call(Call held, data.@this payload, actor.context.@this context)
    {
        var gate = Gates.GetValue(context.App, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            payload.Name = "!data";
            await context.Variable.Set("!data", payload);
            var result = await held.Start(context);
            if (!result.Success) await context.Actor.Channel.Report(result);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            await context.Actor.Channel.Report(context.Error(
                new global::app.error.ServiceError($"{held.Module}.{held.Name} failed: {ex.Message}", "CallFailed") { Exception = ex }));
        }
        finally { gate.Release(); }
    }

    /// <summary>Runs <paramref name="work"/> after any call already running for the same app — an event's bindings
    /// (an element clicked), one at a time with the calls; what it throws is written to the error channel.</summary>
    public static async Task Run(System.Func<Task> work, actor.context.@this context)
    {
        var gate = Gates.GetValue(context.App, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try { await work(); }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            await context.Actor.Channel.Report(context.Error(
                new global::app.error.ServiceError($"An event's bindings failed: {ex.Message}", "EventFailed") { Exception = ex }));
        }
        finally { gate.Release(); }
    }
}
