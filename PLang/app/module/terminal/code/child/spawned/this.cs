using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace app.module.terminal.code.child.spawned;

/// <summary>
/// A program plang spawns itself (Linux, <c>posix_spawn</c>): its standard input, output and error are pipes, and it
/// may get a pipe pair beside them — fd 3 it reads (what is written to it), fd 4 it writes (what it says back) — which
/// .NET's process start can't give (Chromium's <c>--remote-debugging-pipe</c> speaks DevTools there). Its signals start
/// at their defaults, unblocked; it leads a process group of its own, so ending it ends every process it started.
/// Its exit is waited for on a thread of its own. Born on the calling thread: a thread held to permissions (Landlock)
/// spawns a program held to them.
/// </summary>
internal sealed class @this : child.@this
{
    private readonly int _pid;
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private @this(int pid, StreamWriter input, StreamReader output, StreamReader error, Stream? pipe)
    {
        _pid = pid;
        Input = input;
        Output = output;
        Error = error;
        Pipe = pipe;
        new Thread(Wait) { IsBackground = true, Name = "spawned " + pid }.Start();
    }

    internal override int Id => _pid;
    internal override bool HasExited => _exited.Task.IsCompleted;
    internal override int ExitCode => _exited.Task.IsCompleted ? _exited.Task.Result : throw new InvalidOperationException($"{_pid} hasn't exited");
    internal override StreamWriter Input { get; }
    internal override StreamReader Output { get; }
    internal override StreamReader Error { get; }
    internal override Stream? Pipe { get; }

    internal override Task WaitForExitAsync(CancellationToken ct = default) => _exited.Task.WaitAsync(ct);

    /// <summary>Its whole process group: it, and every process it started.</summary>
    internal override void Kill()
    {
        if (!HasExited) Native.kill(-_pid, Native.SigKill);
    }

    public override void Dispose()
    {
        Input.Dispose();
        Output.Dispose();
        Error.Dispose();
        Pipe?.Dispose();
    }

    // its exit: the code it exited with, or 128 + the signal that ended it (as a shell says it)
    private void Wait()
    {
        while (true)
        {
            var waited = Native.waitpid(_pid, out var status, 0);
            if (waited == _pid)
            {
                var signal = status & 0x7f;
                _exited.TrySetResult(signal == 0 ? (status >> 8) & 0xff : 128 + signal);
                return;
            }
            if (Marshal.GetLastPInvokeError() != Native.Interrupted)
            {
                _exited.TrySetResult(-1);   // no such child: someone else reaped it
                return;
            }
        }
    }

    /// <summary>Spawns what <paramref name="info"/> names — its file, arguments, folder and environment — with a pipe
    /// pair at fds 3 and 4 when <paramref name="pipe"/>. Throws when it can't be spawned.</summary>
    internal static @this Start(System.Diagnostics.ProcessStartInfo info, bool pipe)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("A program is spawned with a pipe pair on Linux only.");
        var opened = new List<int>();
        try
        {
            var (inRead, inWrite) = Pair(opened);
            var (outRead, outWrite) = Pair(opened);
            var (errRead, errWrite) = Pair(opened);
            (int read, int write) toChild = default, fromChild = default;
            if (pipe)
            {
                toChild = Pair(opened);
                fromChild = Pair(opened);
            }

            var actions = Marshal.AllocHGlobal(Native.ActionsSize);
            var attributes = Marshal.AllocHGlobal(Native.AttributesSize);
            var signals = Marshal.AllocHGlobal(Native.SignalSetSize);
            var strings = new List<IntPtr>();
            try
            {
                Check(Native.posix_spawn_file_actions_init(actions), "file actions");
                // the child's ends where it looks for them; dup2 clears close-on-exec on the copy, the originals close
                Check(Native.posix_spawn_file_actions_adddup2(actions, inRead, 0), "stdin");
                Check(Native.posix_spawn_file_actions_adddup2(actions, outWrite, 1), "stdout");
                Check(Native.posix_spawn_file_actions_adddup2(actions, errWrite, 2), "stderr");
                if (pipe)
                {
                    Check(Native.posix_spawn_file_actions_adddup2(actions, toChild.read, 3), "fd 3");
                    Check(Native.posix_spawn_file_actions_adddup2(actions, fromChild.write, 4), "fd 4");
                }
                if (!string.IsNullOrEmpty(info.WorkingDirectory))
                    Check(Native.posix_spawn_file_actions_addchdir_np(actions, info.WorkingDirectory), "folder");

                Check(Native.posix_spawnattr_init(attributes), "attributes");
                Native.sigemptyset(signals);
                Check(Native.posix_spawnattr_setsigmask(attributes, signals), "signal mask");
                Native.sigfillset(signals);
                Check(Native.posix_spawnattr_setsigdefault(attributes, signals), "signal defaults");
                Check(Native.posix_spawnattr_setpgroup(attributes, 0), "process group");
                Check(Native.posix_spawnattr_setflags(attributes,
                    Native.SetProcessGroup | Native.SetSignalDefaults | Native.SetSignalMask), "flags");

                var argv = Strings(new[] { info.FileName }.Concat(info.ArgumentList), strings);
                var envp = Strings(info.Environment.Where(e => e.Value != null).Select(e => e.Key + "=" + e.Value), strings);
                var failed = Native.posix_spawn(out var pid, info.FileName, actions, attributes, argv, envp);
                if (failed != 0) throw new InvalidOperationException($"Could not start {info.FileName} (errno {failed})");

                // the child holds its ends now: plang keeps only its own
                foreach (var fd in new[] { inRead, outWrite, errWrite, toChild.read, fromChild.write }.Where(fd => fd > 0))
                {
                    Native.close(fd);
                    opened.Remove(fd);
                }
                var utf8 = new System.Text.UTF8Encoding(false);
                var spawned = new @this(pid,
                    new StreamWriter(Stream(inWrite, FileAccess.Write, opened), info.StandardInputEncoding ?? utf8) { AutoFlush = true },
                    new StreamReader(Stream(outRead, FileAccess.Read, opened), info.StandardOutputEncoding ?? utf8),
                    new StreamReader(Stream(errRead, FileAccess.Read, opened), info.StandardErrorEncoding ?? utf8),
                    pipe ? new Duplex(Stream(fromChild.read, FileAccess.Read, opened), Stream(toChild.write, FileAccess.Write, opened)) : null);
                return spawned;
            }
            finally
            {
                Native.posix_spawn_file_actions_destroy(actions);
                Native.posix_spawnattr_destroy(attributes);
                Marshal.FreeHGlobal(actions);
                Marshal.FreeHGlobal(attributes);
                Marshal.FreeHGlobal(signals);
                foreach (var s in strings) Marshal.FreeCoTaskMem(s);
            }
        }
        catch
        {
            foreach (var fd in opened) Native.close(fd);
            throw;
        }
    }

    // a pipe, both ends closed on exec — and above fd 4, so a dup2 onto 0–4 is always a copy (dup2 of an fd onto
    // itself would leave close-on-exec on, and the child would lose it)
    private static (int read, int write) Pair(List<int> opened)
    {
        var fds = new int[2];
        if (Native.pipe2(fds, Native.CloseOnExec) != 0) throw new InvalidOperationException($"No pipe (errno {Marshal.GetLastPInvokeError()})");
        opened.AddRange(fds);
        return (Above(fds[0], opened), Above(fds[1], opened));
    }

    private static int Above(int fd, List<int> opened)
    {
        if (fd > 4) return fd;
        var moved = Native.fcntl(fd, Native.DuplicateCloseOnExec, 5);
        if (moved < 0) throw new InvalidOperationException($"No fd above 4 (errno {Marshal.GetLastPInvokeError()})");
        Native.close(fd);
        opened.Remove(fd);
        opened.Add(moved);
        return moved;
    }

    // plang's end of a pipe as a stream that closes it
    private static FileStream Stream(int fd, FileAccess access, List<int> opened)
    {
        opened.Remove(fd);
        return new FileStream(new SafeFileHandle(fd, ownsHandle: true), access, bufferSize: 0);
    }

    // a NULL-ended array of C strings, each one freed after the spawn
    private static IntPtr[] Strings(IEnumerable<string> values, List<IntPtr> strings)
    {
        var array = values.Select(v => { var s = Marshal.StringToCoTaskMemUTF8(v); strings.Add(s); return s; }).ToList();
        array.Add(IntPtr.Zero);
        return array.ToArray();
    }

    private static void Check(int error, string what)
    {
        if (error != 0) throw new InvalidOperationException($"posix_spawn's {what} failed (errno {error})");
    }

    /// <summary>The pipe pair as one stream: read from what the program writes (its fd 4), written to what it reads
    /// (its fd 3).</summary>
    private sealed class Duplex(Stream read, Stream write) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => read.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => read.ReadAsync(buffer, ct);
        public override void Write(byte[] buffer, int offset, int count) => write.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => write.WriteAsync(buffer, ct);
        public override void Flush() => write.Flush();
        public override Task FlushAsync(CancellationToken ct) => write.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                read.Dispose();
                write.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>libc's spawn, pipe and wait calls (glibc, x86_64 and arm64).</summary>
    private static class Native
    {
        // glibc's opaque sizes are 80 (file actions) and 336 (attributes); room to spare
        internal const int ActionsSize = 256, AttributesSize = 512, SignalSetSize = 128;
        internal const int CloseOnExec = 0x80000, DuplicateCloseOnExec = 1030, SigKill = 9, Interrupted = 4;
        internal const short SetProcessGroup = 0x02, SetSignalDefaults = 0x04, SetSignalMask = 0x08;

        [DllImport("libc", SetLastError = true)] internal static extern int pipe2(int[] fds, int flags);
        [DllImport("libc", SetLastError = true)] internal static extern int fcntl(int fd, int command, int argument);
        [DllImport("libc")] internal static extern int close(int fd);
        [DllImport("libc", SetLastError = true)] internal static extern int kill(int pid, int signal);
        [DllImport("libc", SetLastError = true)] internal static extern int waitpid(int pid, out int status, int options);
        [DllImport("libc")] internal static extern int sigemptyset(IntPtr set);
        [DllImport("libc")] internal static extern int sigfillset(IntPtr set);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_init(IntPtr actions);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_destroy(IntPtr actions);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_adddup2(IntPtr actions, int fd, int target);
        [DllImport("libc")] internal static extern int posix_spawn_file_actions_addchdir_np(IntPtr actions, string path);
        [DllImport("libc")] internal static extern int posix_spawnattr_init(IntPtr attributes);
        [DllImport("libc")] internal static extern int posix_spawnattr_destroy(IntPtr attributes);
        [DllImport("libc")] internal static extern int posix_spawnattr_setflags(IntPtr attributes, short flags);
        [DllImport("libc")] internal static extern int posix_spawnattr_setpgroup(IntPtr attributes, int group);
        [DllImport("libc")] internal static extern int posix_spawnattr_setsigmask(IntPtr attributes, IntPtr set);
        [DllImport("libc")] internal static extern int posix_spawnattr_setsigdefault(IntPtr attributes, IntPtr set);
        [DllImport("libc")] internal static extern int posix_spawn(out int pid, string path, IntPtr actions, IntPtr attributes,
            IntPtr[] argv, IntPtr[] envp);
    }
}
