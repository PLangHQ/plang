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

    /// <summary>The program a bare name names on <c>PATH</c>, as a shell finds it: each folder in order, the name as given
    /// and, on Windows when it has none, each <c>PATHEXT</c> extension. Only which files exist is asked — never permission.</summary>
    public global::app.type.item.path.file.@this? Find(string name, global::app.actor.context.@this context)
    {
        var folders = (System.Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(global::app.Utils.PathHelper.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var file = context.App.type.list[new global::app.type.@this("path", "file"), context];
        foreach (var folder in folders)
            foreach (var extension in Extensions(name))
            {
                var candidate = new global::app.type.item.path.file.@this(global::app.Utils.PathHelper.Combine(folder, name + extension), file);
                if (context.FileSystem.IsFile(candidate)) return candidate;
            }
        return null;
    }

    // Windows runs "wsl" as "wsl.exe": the name as given, then each PATHEXT extension. Other systems run the name as given.
    private string[] Extensions(string name)
    {
        if (!OperatingSystem.IsWindows() || global::app.Utils.PathHelper.GetExtension(name).Length > 0) return [""];
        var pathExt = System.Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        return [.. pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

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
            return Task.FromResult(context.Ok<Process>(new Process(context.App.type.list[typeof(Process)])
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
