using System.Diagnostics;
using System.Text;
using app.error;
using Call = app.goal.step.action.@this;
using Context = app.actor.context.@this;
using Text = app.type.item.text.@this;

namespace app.module.terminal.type.program;

public sealed partial class @this
{
    // ---- start: run to the end ----------------------------------------------------------------

    /// <summary>Runs it to its end — <c>terminal.start</c>: the value is its stdout; its exit code, stderr, duration,
    /// file and whether the timeout stopped it ride beside. Interactive, it takes this console until it exits;
    /// as administrator (Windows, UAC), only its exit code comes back.</summary>
    internal async Task<global::app.data.@this<Text>> Run(start step)
    {
        var context = asked.Context;
        if (await Prepare() is { } failed) return global::app.data.@this<Text>.From(failed);
        var timeout = Setting.TimeoutInSec.ToDouble();
        using var cts = timeout > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout)) : new CancellationTokenSource();

        if ((await step.Administrator.Value())!.Value)
        {
            if (Sandbox != null)
                return global::app.data.@this<Text>.From(context.Error(new ActionError("A program started as administrator can't be held to permissions.", "PermissionNotEnforced", 400)));
            // Windows starts it through UAC, which passes none of these: a step that gives one is told, not ignored
            var given = new[] { ("Input", step.Input != null), ("OnOutput", step.OnOutput != null),
                ("OnError", step.OnError != null), ("Environment", step.Environment != null) }.Where(g => g.Item2).Select(g => g.Item1).ToList();
            if (given.Count > 0)
                return global::app.data.@this<Text>.From(context.Error(new ActionError(
                    $"A program started as administrator gets no {string.Join(", ", given)}: Windows starts it through UAC, which passes none of them, and only its exit code comes back.",
                    "AdministratorNotSupported", 400)));
            return await Elevated(context, cts.Token);
        }
        if ((await step.Interactive.Value())!.Value) return await Attached(context, cts.Token);
        return await Captured(step, context, cts.Token);
    }

    // It owns this console until it exits: keyboard in, screen out. Nothing is captured.
    private async Task<global::app.data.@this<Text>> Attached(Context context, CancellationToken ct)
    {
        Info!.UseShellExecute = false;
        var watch = Stopwatch.StartNew();
        var started = await asked.Terminal.Start(this, context);
        if (!started.Success) return global::app.data.@this<Text>.From(started);
        using var os = (await started.Value())!.Take();
        var stopped = await Waited(os, ct);
        return Result(context, "", "", stopped ? -1 : os.ExitCode, watch.Elapsed, stopped);
    }

    // Windows elevation goes through the shell (UAC); the shell can't hand back its streams, so only the exit code
    // returns. Environment variables don't pass through runas.
    private async Task<global::app.data.@this<Text>> Elevated(Context context, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return global::app.data.@this<Text>.From(context.Error(new ActionError("Starting a program as administrator is supported on Windows only.", "NotSupported", 400)));
        var elevated = new ProcessStartInfo(Info!.FileName) { UseShellExecute = true, Verb = "runas", WorkingDirectory = Info.WorkingDirectory };
        foreach (var parameter in Info.ArgumentList) elevated.ArgumentList.Add(parameter);
        Info = elevated;
        var watch = Stopwatch.StartNew();
        var started = await asked.Terminal.Start(this, context);
        if (!started.Success) return global::app.data.@this<Text>.From(started);
        using var os = (await started.Value())!.Take();
        var stopped = await Waited(os, ct);
        return Result(context, "", "", stopped ? -1 : os.ExitCode, watch.Elapsed, stopped);
    }

    // Output and error are read line by line. Both streams feed one queue, so the OnOutput and OnError goals run one at
    // a time, in arrival order, on this flow — never two at once. They run inside this step, so they don't take the
    // app's callback gate (a callback holding it could be the one running this step).
    private async Task<global::app.data.@this<Text>> Captured(start step, Context context, CancellationToken ct)
    {
        Redirect();
        // the input as text, written by the value itself — a text its characters, a dict or list its json; what Input
        // names, as it is (a dict stays a dict) — not first made into text, which would lose it
        string? input = null;
        if (step.Input != null && await (await step.Input.Follow(context)).Value() is { } given)
        {
            if (given is Text t) input = t.Clr<string>();
            else
            {
                using var written = new MemoryStream();
                await Text.Encode(written, context.Ok(given), context, null, null, CancellationToken.None);
                input = Encoding.UTF8.GetString(written.ToArray());
            }
        }
        var onOutput = step.OnOutput == null ? null : await step.OnOutput.Value();
        var onError = step.OnError == null ? null : await step.OnError.Value();
        var echo = Setting.Echo.Value;
        var max = (int)Math.Min(Setting.MaxOutputSize.ToDouble(), int.MaxValue);

        var watch = Stopwatch.StartNew();
        var started = await asked.Terminal.Start(this, context);
        if (!started.Success) return global::app.data.@this<Text>.From(started);
        using var os = (await started.Value())!.Take();
        if (input != null) await os.Input.WriteAsync(input);
        os.Input.Close();

        var lines = process.@this.Lines(os);
        var output = new StringBuilder();
        var error = new StringBuilder();
        var stopped = false;
        try
        {
            await foreach (var (isError, line) in lines.ReadAllAsync(ct))
            {
                var kept = isError ? error : output;
                if (kept.Length < max) kept.AppendLine(line);
                if (echo) await context.Actor.Channel[isError ? global::app.channel.list.@this.Error : global::app.channel.list.@this.Output].WriteText(line);
                var call = isError ? onError : onOutput;
                if (call != null) await OnLine(call, line, context);
            }
            await os.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopped = true;
            os.Kill();
        }
        return Result(context, output.ToString().TrimEnd('\r', '\n'), error.ToString().TrimEnd('\r', '\n'),
            stopped ? -1 : os.ExitCode, watch.Elapsed, stopped);
    }

    // ---- open: keep running --------------------------------------------------------------------

    /// <summary>Starts it and returns at once with it running — <c>terminal.open</c>: its lines go to OnOutput and
    /// OnError as they come; binary, or going to a screen, its stdout is messages.</summary>
    internal async Task<global::app.data.@this<process.@this>> Open(open step)
    {
        var context = asked.Context;
        if (await Prepare() is { } failed) return global::app.data.@this<process.@this>.From(failed);
        Redirect();
        var onOutput = step.OnOutput == null ? null : await step.OnOutput.Value();
        var onError = step.OnError == null ? null : await step.OnError.Value();
        Pipe = (await step.Pipe.Value())!.Value;
        if (Pipe && !OperatingSystem.IsLinux())
            return global::app.data.@this<process.@this>.From(context.Error(new ActionError("A program gets a pipe pair on Linux only.", "NotSupported", 400)));

        var started = await asked.Terminal.Start(this, context);
        if (!started.Success) return started;
        var running = (await started.Value())!;
        // binary messages when asked — or when the output goes to a screen, which takes nothing else
        var screen = step.OutputTo == null ? null : await step.OutputTo.Value();
        if ((await step.Binary.Value())!.Value || screen != null) running.ReadMessages(onOutput, screen, context);
        else running.Read(onOutput, onError, context);
        return context.Ok<process.@this>(running);
    }

    // ---- shared ------------------------------------------------------------------------------

    // True when the timeout stopped it.
    private static async Task<bool> Waited(code.child.@this os, CancellationToken ct)
    {
        try { await os.WaitForExitAsync(ct); return false; }
        catch (OperationCanceledException)
        {
            os.Kill();
            return true;
        }
    }

    /// <summary>Runs the held goal call once for one line, the line as <c>%!data%</c>. A failing call is reported on
    /// the error channel and the program keeps running.</summary>
    private static async Task OnLine(Call held, string line, Context context)
    {
        await context.Variable.Set("!data", process.@this.Written(line, context));
        var result = await held.Start(context);
        if (!result.Success) await context.Actor.Channel.Report(result);
    }

    private global::app.data.@this<Text> Result(Context context, string output, string error, int exitCode, TimeSpan duration, bool stopped)
    {
        var result = context.Ok<Text>(output, context.App.type.list["text"]);
        result.Properties["ExitCode"] = exitCode;
        result.Properties["Error"] = error;
        result.Properties["Duration"] = duration.TotalSeconds;
        result.Properties["Program"] = File!.Absolute;
        result.Properties["TimedOut"] = stopped;
        return result;
    }
}
