using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using app.error;
using Call = app.goal.step.action.@this;
using Text = app.type.item.text.@this;
using FilePath = app.type.item.path.file.@this;
using Verb = app.type.item.permission.Verb;

namespace app.module.terminal.code;

/// <summary>
/// Default terminal provider: finds the program, asks the actor for <c>execute</c> when it lives
/// outside the app root, runs it with arguments passed as-is (never through a shell). <c>start</c>
/// runs it to its end and returns stdout; <c>open</c> keeps it running and delivers its lines as
/// they come.
/// </summary>
public sealed class Default : ITerminal
{
    public string Name { get; init; } = "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // ---- start: run to the end ----------------------------------------------------------------

    public async Task<data.@this<Text>> Start(start action)
    {
        var context = action.Context;
        var setting = context.Setting.Of<setting.@this>();
        var (ready, failure) = await Prepare(context, setting, action.App, action.Parameter, action.Environment, action.WorkingDirectory);
        if (ready == null) return data.@this<Text>.From(failure!);
        var (info, program) = ready.Value;

        var timeout = setting.TimeoutInSec.ToDouble();
        using var cts = timeout > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout)) : new CancellationTokenSource();

        if ((await action.Administrator.Value())!.Value) return await Elevated(info, program, context, cts.Token);
        if ((await action.Interactive.Value())!.Value) return await Attached(info, program, context, cts.Token);
        return await Captured(action, info, program, setting, context, cts.Token);
    }

    // The program owns this console until it exits: keyboard in, screen out. Nothing is captured.
    private static async Task<data.@this<Text>> Attached(ProcessStartInfo info, FilePath program, actor.context.@this context, CancellationToken ct)
    {
        info.UseShellExecute = false;
        var watch = Stopwatch.StartNew();
        using var process = System.Diagnostics.Process.Start(info)!;
        Children.Adopt(process);
        var stopped = await WaitAsync(process, ct);
        return Result(context, "", "", stopped ? -1 : process.ExitCode, watch.Elapsed, program, stopped);
    }

    // Windows elevation goes through the shell (UAC); the shell can't hand back the program's
    // streams, so only the exit code returns. Environment variables don't pass through runas.
    private static async Task<data.@this<Text>> Elevated(ProcessStartInfo info, FilePath program, actor.context.@this context, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return data.@this<Text>.From(context.Error(new ActionError("Starting a program as administrator is supported on Windows only.", "NotSupported", 400)));
        var elevated = new ProcessStartInfo(info.FileName) { UseShellExecute = true, Verb = "runas", WorkingDirectory = info.WorkingDirectory };
        foreach (var parameter in info.ArgumentList) elevated.ArgumentList.Add(parameter);
        var watch = Stopwatch.StartNew();
        System.Diagnostics.Process? process;
        try { process = System.Diagnostics.Process.Start(elevated); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return data.@this<Text>.From(context.Error(new ActionError("The user declined administrator rights.", "ElevationDeclined", 403)));
        }
        using (process)
        {
            if (process == null) return data.@this<Text>.From(context.Error(new ActionError($"Could not start {program.Absolute}", "ProcessStartFailed", 500)));
            var stopped = await WaitAsync(process, ct);
            return Result(context, "", "", stopped ? -1 : process.ExitCode, watch.Elapsed, program, stopped);
        }
    }

    // Output and error are read line by line. Both streams feed one queue, so the OnOutput and
    // OnError goals run one at a time, in arrival order, on this flow — never two at once. They run
    // inside this step, so they don't take the app's callback gate (a callback holding it could be
    // the one running this step).
    private static async Task<data.@this<Text>> Captured(start action, ProcessStartInfo info, FilePath program,
        setting.@this setting, actor.context.@this context, CancellationToken ct)
    {
        Redirect(info, setting);
        // the input as text, written by the value itself — a text its characters, a dict or list its json
        string? input = null;
        // what Input names, as it is (a dict stays a dict) — not first made into text, which would lose it
        if (action.Input != null && await (await action.Input.Follow(context)).Value() is { } given)
        {
            if (given is Text t) input = t.Clr<string>();
            else
            {
                using var written = new MemoryStream();
                await Text.Encode(written, context.Ok(given), context, null, null, CancellationToken.None);
                input = Encoding.UTF8.GetString(written.ToArray());
            }
        }
        var onOutput = action.OnOutput == null ? null : await action.OnOutput.Value();
        var onError = action.OnError == null ? null : await action.OnError.Value();
        var echo = setting.Echo.Value;
        var max = (int)Math.Min(setting.MaxOutputSize.ToDouble(), int.MaxValue);

        var watch = Stopwatch.StartNew();
        using var process = System.Diagnostics.Process.Start(info)!;
        Children.Adopt(process);
        if (input != null) await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        var lines = Lines(process);
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
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopped = true;
            process.Kill(entireProcessTree: true);
        }
        return Result(context, output.ToString().TrimEnd('\r', '\n'), error.ToString().TrimEnd('\r', '\n'),
            stopped ? -1 : process.ExitCode, watch.Elapsed, program, stopped);
    }

    // ---- open: keep running --------------------------------------------------------------------

    public async Task<data.@this<Process>> Open(open action)
    {
        var context = action.Context;
        var setting = context.Setting.Of<setting.@this>();
        var (ready, failure) = await Prepare(context, setting, action.App, action.Parameter, action.Environment, action.WorkingDirectory);
        if (ready == null) return data.@this<Process>.From(failure!);
        var (info, program) = ready.Value;
        Redirect(info, setting);
        var onOutput = action.OnOutput == null ? null : await action.OnOutput.Value();
        var onError = action.OnError == null ? null : await action.OnError.Value();

        var os = System.Diagnostics.Process.Start(info)!;
        Children.Adopt(os);   // it ends with this plang, however this plang ends
        var running = new Process { Program = program.Absolute, Id = os.Id, Os = os, Plang = SpeaksPlang(info, context) };

        // binary messages when asked — or when the output goes to a screen, which takes nothing else
        var screen = action.OutputTo == null ? null : await action.OutputTo.Value();
        if ((await action.Binary.Value())!.Value || screen != null)
        {
            // stdout is [u32 length][bytes] messages: each one, as it arrives, straight to the screen
            // it goes to (no goal per message), and to OnOutput as binary when there is one
            _ = Pump(os.StandardError, true, System.Threading.Channels.Channel.CreateUnbounded<(bool, string)>().Writer);
            running.Reading = Task.Run(async () =>
            {
                await foreach (var message in Messages(os.StandardOutput.BaseStream))
                {
                    screen?.Show(message);
                    if (onOutput != null)
                        await global::app.module.on.code.Gate.Call(onOutput,
                            new data.@this("!data", new global::app.type.item.binary.@this(message), context.App.type.list["binary"], context: context), context);
                }
            });
            return context.Ok<Process>(running);
        }

        var lines = Lines(os);
        // Lines are delivered off the step loop, one goal call at a time per app (on.code.Gate).
        running.Reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var (isError, line) in lines.ReadAllAsync())
                {
                    var call = isError ? onError : onOutput;
                    if (call != null) await global::app.module.on.code.Gate.Call(call, await Said(line, running.Plang, context), context);
                }
            }
            // what stops the reading is said — a program whose lines stop arriving without a word is a mystery
            catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
            {
                await Unread(new global::app.error.ServiceError($"Reading {running.Program} stopped: {ex.Message}", "ReadingStopped") { Exception = ex }, context);
            }
        });
        return context.Ok<Process>(running);
    }

    public async Task<data.@this> Send(send action)
    {
        var context = action.Context;
        var running = await action.Process.Value();
        if (running?.Os is not { HasExited: false } os)
            return context.Error(new ActionError($"The program isn't running: {running}", "ProgramNotRunning", 409));
        string line;
        if (running.Plang)
        {
            // to a plang that speaks plang's own format: the Data whole, signed, on one line
            using var written = new MemoryStream();
            // what is sent is the value itself (what %answer% names), signed as it is — never the reference
            var sent = context.Ok(await action.Data.Value());
            var encoded = await PlangFormat(context).Encode(written, sent, context);
            if (!encoded.Success) return encoded;
            line = Encoding.UTF8.GetString(written.ToArray()).TrimEnd('\r', '\n');
        }
        else
        {
            // the value as text, written by itself — a text its characters, a dict or list its json (one line)
            var value = await (await action.Data.Follow(context)).Value();
            if (value is Text t) line = t.Clr<string>() ?? "";
            else
            {
                using var written = new MemoryStream();
                await Text.Encode(written, context.Ok(value), context, null, null, CancellationToken.None);
                line = Encoding.UTF8.GetString(written.ToArray()).ReplaceLineEndings(" ");
            }
        }
        await running.Writing.WaitAsync();
        try
        {
            await os.StandardInput.WriteLineAsync(line);
            await os.StandardInput.FlushAsync();
        }
        catch (IOException ex) { return context.Error(new ActionError($"Could not write to {running.Program}: {ex.Message}", "ProgramNotRunning", 409)); }
        finally { running.Writing.Release(); }
        return context.Ok();
    }

    public async Task<data.@this<global::app.type.item.number.@this>> Wait(wait action)
    {
        var context = action.Context;
        var running = await action.Process.Value();
        if (running?.Os is not { } os)
            return data.@this<global::app.type.item.number.@this>.From(context.Error(new ActionError("No running program to wait for.", "ProgramNotRunning", 409)));
        await os.WaitForExitAsync();
        if (running.Reading != null) await running.Reading;   // the last lines are delivered first
        return context.Ok<global::app.type.item.number.@this>(os.ExitCode);
    }

    public Task<data.@this> Stop(stop action) => StopAsync(action);

    private static async Task<data.@this> StopAsync(stop action)
    {
        var running = await action.Process.Value();
        if (running?.Os is { HasExited: false } os) os.Kill(entireProcessTree: true);
        return action.Context.Ok();
    }

    // ---- shared ------------------------------------------------------------------------------

    /// <summary>The program found and permitted, with its arguments, environment and folder; or why not.</summary>
    private static async Task<((ProcessStartInfo info, FilePath program)? ready, data.@this? failure)> Prepare(
        actor.context.@this context, setting.@this setting, data.@this<Text> app,
        data.@this<global::app.type.item.list.@this>? parameter, data.@this<global::app.type.item.dict.@this>? environment,
        data.@this<global::app.type.item.path.@this>? workingDirectory)
    {
        var name = (await app.Value())!.Clr<string>()!;
        var program = FilePath.Program(name, context);
        if (program == null)
            return (null, context.Error(new ActionError($"Program not found: {name}. Not a path, and not on PATH.", "ProgramNotFound", 404)));
        var allowed = await program.Authorize(Verb.Execute, context);
        if (allowed.Exits || !allowed.Success) return (null, allowed);

        var folder = workingDirectory == null ? null : await workingDirectory.Value();
        if (folder != null)
        {
            var readable = await folder.Authorize(Verb.Read, context);
            if (readable.Exits || !readable.Success) return (null, readable);
        }

        var info = new ProcessStartInfo(program.Absolute) { WorkingDirectory = folder?.Absolute ?? context.App.AbsolutePath };
        var parameters = parameter == null || !await parameter.ToBooleanAsync() ? null : (await parameter.Value())!.Clr<List<object?>>();
        foreach (var p in parameters ?? []) info.ArgumentList.Add(p?.ToString() ?? "");

        var env = setting.Environment.Clr<Dictionary<string, object?>>() ?? new();
        if (environment != null && await environment.ToBooleanAsync())
            foreach (var (key, value) in (await environment.Value())!.Clr<Dictionary<string, object?>>() ?? new())
                env[key] = value;
        foreach (var (key, value) in env) info.Environment[key] = value?.ToString();
        return ((info, program), null);
    }

    // stdin is always the program's own: written by plang, never inherited — inherited, it would
    // read plang's console, taking keystrokes meant for plang's own prompts.
    private static void Redirect(ProcessStartInfo info, setting.@this setting)
    {
        var encoding = Encoding.GetEncoding((string)setting.Encoding.Clr<string>()!);
        info.UseShellExecute = false;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.StandardOutputEncoding = encoding;
        info.StandardErrorEncoding = encoding;
        info.StandardInputEncoding = new UTF8Encoding(false);
    }

    /// <summary>stdout and stderr as one queue of lines, completed when both streams end.</summary>
    private static ChannelReader<(bool error, string line)> Lines(System.Diagnostics.Process process)
    {
        var lines = System.Threading.Channels.Channel.CreateUnbounded<(bool error, string line)>();
        _ = Task.WhenAll(
                Pump(process.StandardOutput, false, lines.Writer),
                Pump(process.StandardError, true, lines.Writer))
            .ContinueWith(_ => lines.Writer.Complete(), TaskScheduler.Default);
        return lines.Reader;
    }

    /// <summary>Length-prefixed binary messages: [u32 length, little-endian][that many bytes].</summary>
    private static async IAsyncEnumerable<byte[]> Messages(System.IO.Stream stream)
    {
        var header = new byte[4];
        while (true)
        {
            try { await stream.ReadExactlyAsync(header); }
            catch (EndOfStreamException) { yield break; }
            var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is < 0 or > 256 * 1024 * 1024) yield break;   // not our framing: stop, don't allocate garbage
            var message = new byte[length];
            try { await stream.ReadExactlyAsync(message); }
            catch (EndOfStreamException) { yield break; }
            yield return message;
        }
    }

    private static async Task Pump(StreamReader reader, bool isError, ChannelWriter<(bool, string)> writer)
    {
        while (await reader.ReadLineAsync() is { } line)
            await writer.WriteAsync((isError, line));
    }

    // True when the timeout stopped the program.
    private static async Task<bool> WaitAsync(System.Diagnostics.Process process, CancellationToken ct)
    {
        try { await process.WaitForExitAsync(ct); return false; }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return true;
        }
    }

    private static data.@this Line(string line, actor.context.@this context)
        => new("!data", line, context.App.type.list["text"], context: context);

    // plang's own format — the one a plang started with --app.type.format=application/plang speaks
    private static global::app.type.kind.@this PlangFormat(actor.context.@this context)
        => context.App.type.list.Mime("application/plang");

    // The program is a plang told to speak plang's own format (--app.type.format=…, a name of that format).
    private static bool SpeaksPlang(ProcessStartInfo info, actor.context.@this context)
    {
        const string flag = "--app.type.format=";
        var named = info.ArgumentList.FirstOrDefault(a => a.StartsWith(flag, StringComparison.OrdinalIgnoreCase))?[flag.Length..];
        return named != null && ReferenceEquals(context.App.type.Named(named), PlangFormat(context));
    }

    // What the program said, as %!data%: from a plang that speaks plang's own format, a line that is a Data is that
    // Data whole (an ask is an Ask — `if %!data.type% == "ask"`); any other line is its text.
    private static async Task<data.@this> Said(string line, bool plang, actor.context.@this context)
    {
        if (!plang || !line.TrimStart().StartsWith('{')) return Line(line, context);
        try
        {
            var data = await PlangFormat(context).Decode(Encoding.UTF8.GetBytes(line), context, "!data");
            // read now, so the goal reading it sees the value itself — an ask from the program is its question,
            // not this goal's own ask waiting
            if (data.Success) { await data.Value(); data.Name = "!data"; return data; }
            await Unread(data.Error!, context);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            await Unread(new global::app.error.ServiceError($"plang's own format didn't read: {ex.Message}", "PlangUnread") { Exception = ex }, context);
        }
        return Line(line, context);   // what couldn't be read as Data still arrives, as its text
    }

    // A line from a plang that didn't read as Data: said on the error channel, never swallowed.
    private static Task Unread(global::app.error.Error why, actor.context.@this context)
        => context.Actor.Channel.Report(context.Error(why));

    /// <summary>Runs the held goal call once for one line, the line as <c>%!data%</c>. A failing call is
    /// reported on the error channel and the program keeps running.</summary>
    private static async Task OnLine(Call held, string line, actor.context.@this context)
    {
        await context.Variable.Set("!data", Line(line, context));
        var result = await held.Start(context);
        if (!result.Success) await context.Actor.Channel.Report(result);
    }

    private static data.@this<Text> Result(actor.context.@this context, string output, string error, int exitCode,
        TimeSpan duration, FilePath program, bool stopped)
    {
        var result = context.Ok<Text>(output, context.App.type.list["text"]);
        result.Properties["ExitCode"] = exitCode;
        result.Properties["Error"] = error;
        result.Properties["Duration"] = duration.TotalSeconds;
        result.Properties["Program"] = program.Absolute;
        result.Properties["TimedOut"] = stopped;
        return result;
    }
}
