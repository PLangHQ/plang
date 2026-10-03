namespace app.module.terminal.code.child;

/// <summary>
/// The operating system's process a running program is: its id, its standard input, output and error, its exit. Two
/// kinds: <see cref="managed.@this"/> (.NET starts it — every program, on every system) and <see cref="spawned.@this"/>
/// (plang spawns it itself, Linux: a program that also gets a pipe pair beside its standard streams, as Chromium's
/// DevTools does on fds 3 and 4).
/// </summary>
internal abstract class @this : IDisposable
{
    /// <summary>The operating system's process id.</summary>
    internal abstract int Id { get; }

    internal abstract bool HasExited { get; }

    /// <summary>Its exit code, once it has exited.</summary>
    internal abstract int ExitCode { get; }

    /// <summary>What is written to it (its stdin).</summary>
    internal abstract StreamWriter Input { get; }

    /// <summary>What it writes (its stdout) and its errors (its stderr).</summary>
    internal abstract StreamReader Output { get; }
    internal abstract StreamReader Error { get; }

    /// <summary>The pipe pair beside its standard streams — written to its fd 3, read from its fd 4 — when it has one.</summary>
    internal virtual Stream? Pipe => null;

    internal abstract Task WaitForExitAsync(CancellationToken ct = default);

    /// <summary>Ends it, and every process it started.</summary>
    internal abstract void Kill();

    public abstract void Dispose();
}
