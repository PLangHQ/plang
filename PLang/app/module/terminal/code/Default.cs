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
        var (ready, failure) = await Prepare(context, setting, action.App, action.Parameter, action.Environment, action.WorkingDirectory, action.Permission);
        if (ready == null) return data.@this<Text>.From(failure!);
        var (info, program, sandbox, trusted) = ready.Value;
        // a start trusted by its origin runs by the terminal's defaults, none of the actor's settings (579)
        if (trusted) setting = new();

        var timeout = setting.TimeoutInSec.ToDouble();
        using var cts = timeout > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout)) : new CancellationTokenSource();

        if ((await action.Administrator.Value())!.Value)
        {
            if (sandbox != null)
                return data.@this<Text>.From(context.Error(new ActionError("A program started as administrator can't be held to permissions.", "PermissionNotEnforced", 400)));
            // Windows starts it through UAC, which passes none of these: a step that gives one is told, not ignored
            var given = new[] { ("Input", action.Input != null), ("OnOutput", action.OnOutput != null),
                ("OnError", action.OnError != null), ("Environment", action.Environment != null) }.Where(g => g.Item2).Select(g => g.Item1).ToList();
            if (given.Count > 0)
                return data.@this<Text>.From(context.Error(new ActionError(
                    $"A program started as administrator gets no {string.Join(", ", given)}: Windows starts it through UAC, which passes none of them, and only its exit code comes back.",
                    "AdministratorNotSupported", 400)));
            return await Elevated(info, program, context, cts.Token);
        }
        if ((await action.Interactive.Value())!.Value) return await Attached(info, program, sandbox, context, cts.Token);
        return await Captured(action, info, program, sandbox, setting, context, cts.Token);
    }

    // The program owns this console until it exits: keyboard in, screen out. Nothing is captured.
    private static async Task<data.@this<Text>> Attached(ProcessStartInfo info, FilePath program, Sandbox? sandbox, actor.context.@this context, CancellationToken ct)
    {
        info.UseShellExecute = false;
        var watch = Stopwatch.StartNew();
        var started = await Launch(info, program, sandbox, pipe: false, context);
        if (!started.Success) return data.@this<Text>.From(started);
        using var process = (await started.Value())!.Take();
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
        if (process == null) return data.@this<Text>.From(context.Error(new ActionError($"Could not start {program.Absolute}", "ProcessStartFailed", 500)));
        using var elevatedChild = new child.managed.@this(process);
        var stopped = await WaitAsync(elevatedChild, ct);
        return Result(context, "", "", stopped ? -1 : elevatedChild.ExitCode, watch.Elapsed, program, stopped);
    }

    // Output and error are read line by line. Both streams feed one queue, so the OnOutput and
    // OnError goals run one at a time, in arrival order, on this flow — never two at once. They run
    // inside this step, so they don't take the app's callback gate (a callback holding it could be
    // the one running this step).
    private static async Task<data.@this<Text>> Captured(start action, ProcessStartInfo info, FilePath program, Sandbox? sandbox,
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
        var started = await Launch(info, program, sandbox, pipe: false, context);
        if (!started.Success) return data.@this<Text>.From(started);
        using var process = (await started.Value())!.Take();
        Children.Adopt(process);
        if (input != null) await process.Input.WriteAsync(input);
        process.Input.Close();

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
            process.Kill();
        }
        return Result(context, output.ToString().TrimEnd('\r', '\n'), error.ToString().TrimEnd('\r', '\n'),
            stopped ? -1 : process.ExitCode, watch.Elapsed, program, stopped);
    }

    // ---- open: keep running --------------------------------------------------------------------

    public async Task<data.@this<Process>> Open(open action)
    {
        var context = action.Context;
        var setting = context.Setting.Of<setting.@this>();
        var (ready, failure) = await Prepare(context, setting, action.App, action.Parameter, action.Environment, action.WorkingDirectory, action.Permission);
        if (ready == null) return data.@this<Process>.From(failure!);
        var (info, program, sandbox, trusted) = ready.Value;
        // a start trusted by its origin runs by the terminal's defaults, none of the actor's settings (579)
        if (trusted) setting = new();
        Redirect(info, setting);
        var onOutput = action.OnOutput == null ? null : await action.OnOutput.Value();
        var onError = action.OnError == null ? null : await action.OnError.Value();
        var pipe = (await action.Pipe.Value())!.Value;
        if (pipe && !OperatingSystem.IsLinux())
            return data.@this<Process>.From(context.Error(new ActionError("A program gets a pipe pair on Linux only.", "NotSupported", 400)));

        var started = await Launch(info, program, sandbox, pipe, context);
        if (!started.Success) return started;
        var launched = (await started.Value())!;
        var os = launched.Os!;
        Children.Adopt(os);   // it ends with this plang, however this plang ends
        var running = new Process { Program = program.Absolute, Id = os.Id, Os = os, Plang = SpeaksPlang(info, context), pipe = launched.pipe };

        // binary messages when asked — or when the output goes to a screen, which takes nothing else
        var screen = action.OutputTo == null ? null : await action.OutputTo.Value();
        if ((await action.Binary.Value())!.Value || screen != null)
        {
            // stdout is [u32 length][bytes] messages: each one, as it arrives, straight to the screen
            // it goes to (no goal per message), and to OnOutput as binary when there is one
            // stderr is text: its last lines are kept (what it said, if it stops)
            _ = Task.Run(async () =>
            {
                try { while (await os.Error.ReadLineAsync() is { } line) running.Heard(line); }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
            });
            // a plang behind it takes calls to its goals (%container.goal["Question"]%): a call goes down its input as a
            // line; its answer comes back as a message of its own (kind 9, {"reply": …}) to the call waiting on it
            running.Remote.Link = async line =>
            {
                await running.Writing.WaitAsync();
                try { await os.Input.WriteLineAsync(line); await os.Input.FlushAsync(); }
                finally { running.Writing.Release(); }
            };
            running.Reading = Task.Run(async () =>
            {
                await foreach (var message in Messages(os.Output.BaseStream))
                {
                    if (Reply(message) is { } reply)
                    {
                        running.Remote.Answered(reply);
                        continue;
                    }
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
                    if (isError) running.Heard(line);   // its last lines on stderr are kept
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
            await os.Input.WriteLineAsync(line);
            await os.Input.FlushAsync();
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
        if (running?.Os is { HasExited: false } os) os.Kill();
        return action.Context.Ok();
    }

    // ---- shared ------------------------------------------------------------------------------

    /// <summary>The program found and permitted, with its arguments, environment and folder, and the hold on it when
    /// the step gives it permissions, and whether it is trusted by its origin (579); or why not.</summary>
    private static async Task<((ProcessStartInfo info, FilePath program, Sandbox? sandbox, bool trusted)? ready, data.@this? failure)> Prepare(
        actor.context.@this context, setting.@this setting, data.@this<Text> app,
        data.@this<global::app.type.item.list.@this>? parameter, data.@this<global::app.type.item.dict.@this>? environment,
        data.@this<global::app.type.item.path.@this>? workingDirectory,
        data.@this<global::app.type.item.list.@this<global::app.type.item.permission.@this>>? permission)
    {
        // what the step wrote, before it is read: a %ref% names permissions even when it holds none
        var named = permission?.Peek() is { } written and not global::app.type.item.@null.@this ? written : null;
        var given = permission == null ? null : await permission.Value();
        // permissions named but not read (one that is no permission, a %ref% holding none) stop the start — a step that
        // gives a program permissions never runs it free; to run free, the step leaves Permission out
        if (permission != null && (!permission.Success || permission.Error != null))
            return (null, permission.Error != null ? context.Error(permission.Error) : permission);
        if (given == null && named != null)
            return (null, context.Error(new ActionError(
                $"The step gives the program permissions, but {named} holds none — leave Permission out to run it free.", "PermissionInvalid", 400)));
        var (ready, failure) = await Prepare(context, setting, app, parameter, environment, workingDirectory, held: given != null);
        if (ready == null) return (null, failure);
        if (given == null) return ((ready.Value.info, ready.Value.program, null, ready.Value.trusted), null);
        var (held, refused) = await Sandbox.Of(given.Rows(context), ready.Value.program, context);
        return held == null ? (null, refused) : ((ready.Value.info, ready.Value.program, held, ready.Value.trusted), null);
    }

    /// <summary>The program started — held to its permissions when it has some, spawned with a pipe pair when it asks
    /// for one; or why it isn't.</summary>
    private static Task<data.@this<Process>> Launch(ProcessStartInfo info, FilePath program, Sandbox? sandbox, bool pipe,
        actor.context.@this context)
    {
        child.@this Started() => pipe
            ? child.spawned.@this.Start(info, pipe: true)
            : new child.managed.@this(System.Diagnostics.Process.Start(info)
                ?? throw new InvalidOperationException($"Could not start {info.FileName}"));
        try
        {
            var os = sandbox?.Start(Started) ?? Started();
            // the pipe pair is a channel of the program's, belonging to the actor that started it and nobody else (not
            // listed among its named channels: each program has its own): messages each ended — a newline, until the
            // one who reads it sets another end
            var channel = os.Pipe is { } pair
                ? new global::app.channel.type.stream.@this("pipe", pair, ownsStream: true) { Framed = true, Actor = context.Actor! }
                : null;
            return Task.FromResult(context.Ok<Process>(new Process { Program = program.Absolute, Id = os.Id, Os = os, pipe = channel }));
        }
        catch (Exception ex) when (pipe && sandbox == null && ex is InvalidOperationException or PlatformNotSupportedException)
        {
            return Task.FromResult(data.@this<Process>.From(context.Error(new ActionError(
                $"Could not start {program.Absolute}: {ex.Message}", "ProcessStartFailed", 500))));
        }
        // the kernel couldn't hold it: it isn't started, never started free
        catch (Exception ex) when (sandbox != null && ex is not (OutOfMemoryException or StackOverflowException))
        {
            return Task.FromResult(data.@this<Process>.From(context.Error(new ActionError(
                $"Could not hold {program.Absolute} to its permissions: {ex.Message}", "PermissionNotEnforced", 500))));
        }
    }

    private static async Task<((ProcessStartInfo info, FilePath program, bool trusted)? ready, data.@this? failure)> Prepare(
        actor.context.@this context, setting.@this setting, data.@this<Text> app,
        data.@this<global::app.type.item.list.@this>? parameter, data.@this<global::app.type.item.dict.@this>? environment,
        data.@this<global::app.type.item.path.@this>? workingDirectory, bool held = false)
    {
        // the step names what it starts itself when the program, its arguments, environment and folder are all written
        // in it — no variable but the running app's anchors (%!app.AbsolutePath%), nothing its caller handed it: only
        // then may a goal that ships with plang start it unasked (decision 579; any other variable there is the
        // caller's, and is asked as the caller)
        static bool Written(data.@this? given)
            => given?.Peek() is not { } held || held.Variable.All(global::app.type.item.path.@this.IsAnchor);
        var named = Written(app) && Written(parameter) && Written(environment) && Written(workingDirectory);
        // an option the step doesn't write is read from the actor's settings (terminal.start.setting.<option>, then
        // terminal.setting.<option>): for a start that would be trusted, that is the actor choosing what runs — the
        // folder, the arguments, the environment — so a start is the step's own only when no setting gives it an option
        if (named && context.call.Current?.Action is { } asking && asking.Module[asking.Name] is { } element)
            foreach (var option in element.Property)
                if (asking.Property[option.Name] == null
                    && (await context.Setting.Get(asking, option.Name.ToLowerInvariant())).IsInitialized)
                {
                    named = false;
                    break;
                }

        var name = (await app.Value())!.Clr<string>()!;
        var program = FilePath.Program(name, context);
        if (program == null)
            return (null, context.Error(new ActionError($"Program not found: {name}. Not a path, and not on PATH.", "ProgramNotFound", 404)));
        var allowed = await program.Authorize(Verb.execute, context, named);
        if (allowed.Exits || !allowed.Success) return (null, allowed);

        var folder = workingDirectory == null ? null : await workingDirectory.Value();
        if (folder != null)
        {
            var readable = await folder.Authorize(Verb.read, context);
            if (readable.Exits || !readable.Success) return (null, readable);
        }

        var info = new ProcessStartInfo(program.Absolute) { WorkingDirectory = folder?.Absolute ?? context.App.AbsolutePath };
        var parameters = parameter == null || !await parameter.ToBooleanAsync() ? null : (await parameter.Value())!.Clr<List<object?>>();
        foreach (var p in parameters ?? []) info.ArgumentList.Add(p?.ToString() ?? "");

        // a program held to permissions starts with none of plang's own environment (its keys among it): only the
        // sandbox's base, and what the settings and the step give it
        if (held)
        {
            info.Environment.Clear();
            foreach (var (key, value) in Sandbox.Environment) info.Environment[key] = value;
        }
        // a start trusted by its origin (an os goal naming it all) takes nothing of the actor's settings that changes what
        // runs: the settings are the actor's, a user program sets them, and an LD_PRELOAD there would run the user's code
        // inside what started unasked. Its environment is plang's own and what the step names, nothing else.
        var trusted = named && global::app.type.item.path.@this.AskedByOs(context);
        var env = trusted ? new() : setting.Environment.Clr<Dictionary<string, object?>>() ?? new();
        // a step that writes no Environment reads the setting's through its property — for a trusted start that is the
        // same side door: it takes only what the step itself wrote
        var written = environment?.Peek() is { IsNull: false };
        if (environment != null && (written || !trusted) && await environment.ToBooleanAsync())
            foreach (var (key, value) in (await environment.Value())!.Clr<Dictionary<string, object?>>() ?? new())
                env[key] = value;
        foreach (var (key, value) in env) info.Environment[key] = value?.ToString();
        return ((info, program, trusted), null);
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
    private static ChannelReader<(bool error, string line)> Lines(child.@this process)
    {
        var lines = System.Threading.Channels.Channel.CreateUnbounded<(bool error, string line)>();
        _ = Task.WhenAll(
                Pump(process.Output, false, lines.Writer),
                Pump(process.Error, true, lines.Writer))
            .ContinueWith(_ => lines.Writer.Complete(), TaskScheduler.Default);
        return lines.Reader;
    }

    /// <summary>A message up from a plang behind it that answers a call to one of its goals: kind 9, <c>{"reply": …}</c>.</summary>
    private static System.Text.Json.JsonElement? Reply(byte[] message)
    {
        if (message.Length < 10 || message[0] != 9 || message[1] != (byte)'{') return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(message.AsMemory(1));
            return doc.RootElement.TryGetProperty("reply", out var reply) ? reply.Clone() : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
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
    private static async Task<bool> WaitAsync(child.@this process, CancellationToken ct)
    {
        try { await process.WaitForExitAsync(ct); return false; }
        catch (OperationCanceledException)
        {
            process.Kill();
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
