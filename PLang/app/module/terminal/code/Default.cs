using app.error;
using Process = app.module.terminal.type.process.@this;

namespace app.module.terminal.code;

/// <summary>
/// Default terminal provider: spawns the program — .NET starts it, or plang spawns it itself when it gets a pipe pair
/// (Linux) — held to its permissions on the Landlock-locked thread when it has some, and adopted so it ends with this
/// plang (Windows' job).
/// </summary>
public sealed class Default : ITerminal
{
    public string Name { get; init; } = "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // the programs it started: on Windows, they end with this plang
    private readonly child.list.@this _child = new();

    public Task<data.@this<Process>> Start(type.program.@this program, global::app.actor.context.@this context)
    {
        var (info, file, sandbox, pipe) = (program.Info!, program.File!, program.Sandbox, program.Pipe);
        child.@this Started() => pipe
            ? child.spawned.@this.Start(info, pipe: true)
            : new child.managed.@this(System.Diagnostics.Process.Start(info)
                ?? throw new InvalidOperationException($"Could not start {info.FileName}"));
        try
        {
            var os = sandbox?.Start(Started) ?? Started();
            _child.Add(os);   // it ends with this plang, however this plang ends
            // the pipe pair is a channel of the program's, belonging to the actor that started it and nobody else (not
            // listed among its named channels: each program has its own): messages each ended — a newline, until the
            // one who reads it sets another end
            var channel = os.Pipe is { } pair
                ? new global::app.channel.type.stream.@this("pipe", pair, ownsStream: true) { Framed = true, Actor = context.Actor! }
                : null;
            return Task.FromResult(context.Ok<Process>(new Process
            {
                Program = file.Absolute, Id = os.Id, Os = os, pipe = channel,
                Plang = program.SpeaksPlang(context),
                OutputEncoding = program.Setting.Encoding.Clr<string>() ?? "utf-8", StartedBy = context.Actor,
            }));
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Task.FromResult(data.@this<Process>.From(context.Error(new ActionError("The user declined administrator rights.", "ElevationDeclined", 403))));
        }
        catch (Exception ex) when (sandbox == null && ex is InvalidOperationException or PlatformNotSupportedException)
        {
            return Task.FromResult(data.@this<Process>.From(context.Error(new ActionError(
                $"Could not start {file.Absolute}: {ex.Message}", "ProcessStartFailed", 500))));
        }
        // the kernel couldn't hold it: it isn't started, never started free
        catch (Exception ex) when (sandbox != null && ex is not (OutOfMemoryException or StackOverflowException))
        {
            return Task.FromResult(data.@this<Process>.From(context.Error(new ActionError(
                $"Could not hold {file.Absolute} to its permissions: {ex.Message}", "PermissionNotEnforced", 500))));
        }
    }
}
