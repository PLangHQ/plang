using System.Runtime.InteropServices;

namespace app.module.terminal.code;

/// <summary>
/// The folders a program may touch, enforced by the kernel (Linux Landlock): what it may read, what it may
/// write, and nothing else — not even what plang itself may. A fresh thread locks itself to them and starts the
/// program there: a process is born with the lock of the thread that started it, and keeps it through exec,
/// so the program can never lift it, while the rest of plang never was locked. The thread ends with the start.
/// Fails closed: where the lock can't be made (not Linux, no Landlock), the program isn't started.
/// </summary>
internal sealed class Sandbox
{
    /// <summary>Read (and run) only: what a program needs to start — its libraries and the system's own
    /// settings. Not /proc: there it would read plang's own environment.</summary>
    private static readonly string[] Base = ["/usr", "/lib", "/lib64", "/bin", "/sbin", "/etc", "/sys", "/dev"];
    /// <summary>Written by most programs, holding nothing.</summary>
    private static readonly string[] Sink = ["/dev/null"];   // written: the one system path a program may write

    /// <summary>The program file: read and run, nothing else of its folder.</summary>
    public string Program { get; }
    public IReadOnlyList<string> Read { get; }
    public IReadOnlyList<string> Write { get; }

    /// <summary>Each one an absolute path, from a path the caller may touch (<c>path.Absolute</c>).</summary>
    public Sandbox(string program, IEnumerable<string> read, IEnumerable<string> write)
    {
        Program = program;
        Read = read.Distinct().ToList();
        Write = write.Distinct().ToList();
    }

    /// <summary>Why a sandbox can't be made here, or null when it can.</summary>
    public static string? Unavailable()
    {
        if (!OperatingSystem.IsLinux()) return "Running a program in a sandbox is supported on Linux only.";
        var abi = Landlock.Version();
        return abi < 3 ? $"This kernel can't hold a program to its folders (Landlock {(abi < 0 ? "is off" : $"version {abi}, needs 3")})." : null;
    }

    /// <summary>Starts the program inside its folders. Throws when the lock can't be made: it never runs unlocked.</summary>
    public System.Diagnostics.Process Start(System.Diagnostics.ProcessStartInfo info)
    {
        // what the thread started, or why it couldn't — set once, by the thread
        var started = new TaskCompletionSource<System.Diagnostics.Process>();
        var thread = new Thread(() =>
        {
            try
            {
                // the base where this system has it; the program file and every folder the caller named
                Landlock.Restrict(Base.Concat(Sink), Read.Prepend(Program), Write);
                started.SetResult(System.Diagnostics.Process.Start(info)
                    ?? throw new InvalidOperationException($"Could not start {info.FileName}"));
            }
            catch (Exception ex) { started.SetException(ex); }
        }) { IsBackground = true, Name = "sandbox" };
        thread.Start();
        thread.Join();
        return started.Task.GetAwaiter().GetResult();
    }

    /// <summary>The Landlock calls (x86_64/arm64 numbers are the same: 444–446).</summary>
    private static class Landlock
    {
        [DllImport("libc", SetLastError = true)] private static extern long syscall(long number, IntPtr attr, nuint size, uint flags);
        [DllImport("libc", SetLastError = true)] private static extern long syscall(long number, ref ulong attr, nuint size, uint flags);
        [DllImport("libc", SetLastError = true)] private static extern long syscall(long number, int ruleset, int type, ref PathBeneath rule, uint flags);
        [DllImport("libc", SetLastError = true)] private static extern long syscall(long number, int ruleset, uint flags);
        [DllImport("libc", SetLastError = true)] private static extern int prctl(int option, ulong a, ulong b, ulong c, ulong d);
        [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
        [DllImport("libc")] private static extern int close(int fd);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PathBeneath { public ulong Allowed; public int Folder; }

        private const long Create = 444, Add = 445, RestrictSelf = 446;
        private const int PathBeneathRule = 1, NoNewPrivileges = 38, OPath = 0x200000, OCloseOnExec = 0x80000;
        // execute, read file, read dir
        private const ulong Reading = 1 | 4 | 8;
        // + write file, remove dir, remove file, make dir, make file, truncate
        // + make symlink (4096): a checkout of a repo that holds a link makes one (git, tar). Safe: the kernel checks
        //   the path a link leads to when it is opened, so a link can't lead out of the jail
        // + refer (8192): a move or link from one folder to another (git mv d1/y d2/y; without it EXDEV, "Invalid
        //   cross-device link"). Safe: the kernel moves a file only where the jail grants it at least the same rights
        private const ulong Writing = Reading | 2 | 16 | 32 | 128 | 256 | 16384 | 4096 | 8192;
        // + make char/sock/fifo/block: every right Landlock 3 guards — what no rule names is refused
        private const ulong Guarded = Writing | 64 | 512 | 1024 | 2048;

        public static int Version() => (int)syscall(Create, IntPtr.Zero, 0, 1);

        /// <summary>Locks the calling thread: <paramref name="system"/> read where this system has it, the named folders
        /// read or written — a named one that can't be held fails the lock.</summary>
        public static void Restrict(IEnumerable<string> system, IEnumerable<string> read, IEnumerable<string> write)
        {
            ulong guarded = Guarded;
            var ruleset = (int)syscall(Create, ref guarded, sizeof(ulong), 0);
            if (ruleset < 0) throw new InvalidOperationException($"Landlock refused a ruleset (errno {Marshal.GetLastPInvokeError()})");
            try
            {
                foreach (var path in system) Allow(ruleset, path, path == "/dev/null" ? Writing : Reading, required: false);
                foreach (var path in read) Allow(ruleset, path, Reading, required: true);
                foreach (var path in write) Allow(ruleset, path, Writing, required: true);
                if (prctl(NoNewPrivileges, 1, 0, 0, 0) != 0) throw new InvalidOperationException("no_new_privs was refused");
                if (syscall(RestrictSelf, ruleset, 0) < 0) throw new InvalidOperationException($"Landlock refused the lock (errno {Marshal.GetLastPInvokeError()})");
            }
            finally { close(ruleset); }
        }

        private const int NoSuchFile = 2, Invalid = 22;
        // what a file (not a folder) can be given: execute, write, read, truncate
        private const ulong FileRights = 1 | 2 | 4 | 16384;

        private static void Allow(int ruleset, string path, ulong access, bool required)
        {
            var at = open(path, OPath | OCloseOnExec);
            if (at < 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                if (!required && errno == NoSuchFile) return;   // a system folder this system doesn't have
                throw new InvalidOperationException($"Can't open {path} for the lock (errno {errno})");
            }
            try
            {
                var rule = new PathBeneath { Allowed = access, Folder = at };
                if (syscall(Add, ruleset, PathBeneathRule, ref rule, 0) >= 0) return;
                // a file takes only a file's rights: the kernel says so with EINVAL
                if (Marshal.GetLastPInvokeError() == Invalid)
                {
                    rule.Allowed = access & FileRights;
                    if (syscall(Add, ruleset, PathBeneathRule, ref rule, 0) >= 0) return;
                }
                throw new InvalidOperationException($"Landlock refused {path} (errno {Marshal.GetLastPInvokeError()})");
            }
            finally { close(at); }
        }
    }
}
