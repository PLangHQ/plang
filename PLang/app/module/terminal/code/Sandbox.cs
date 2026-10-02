using System.Runtime.InteropServices;
using Permission = app.type.item.permission.@this;
using Verb = app.type.item.permission.Verb;
using FilePath = app.type.item.path.file.@this;
using PathItem = app.type.item.path.@this;

namespace app.module.terminal.code;

/// <summary>
/// The terminal's way of holding a program to its permissions (<c>terminal.start(…, Permission=[{path, verbs}])</c>),
/// enforced by the kernel (Linux Landlock): the program touches the folders its permissions name, with their verbs,
/// and nothing else — not even what plang itself may. A fresh thread locks itself to them and starts the program
/// there: a process is born with the lock of the thread that started it, and keeps it through exec, so the program
/// can never lift it, while the rest of plang never was locked. The thread ends with the start.
///
/// <para>Each permission is first the caller's to grant (asked on the caller's channel when it isn't yet): a program
/// is never given more than the app may touch itself. The kernel holds folders, named exactly: a glob or regex
/// permission is refused. A verb is its rights: read — read files, list folders; write — write, make, remove, move and
/// link (a checkout makes symlinks; <c>git mv</c> moves between folders); delete — remove. Execute is not given to a
/// program yet. Besides its permissions a program reads its own file and the system's libraries (/usr /lib /lib64
/// /bin /sbin /etc /sys /dev — not /proc, where it would read plang's own environment) and writes /dev/null. No /tmp:
/// a program that writes temporary files needs a permission for a folder (and to be told of it, e.g. TMPDIR).</para>
///
/// <para>Fails closed: where the lock can't be made (not Linux, no Landlock 3, a folder that can't be held), the
/// program isn't started. Not held yet: the program runs as plang's own user, so it can signal plang (Landlock's
/// scoping of signals comes with Linux 6.12; WSL has 6.6), and it reaches the network freely — network permissions
/// are to come.</para>
/// </summary>
internal sealed class Sandbox
{
    /// <summary>Read (and run) only: what a program needs to start — its libraries and the system's own
    /// settings. Not /proc: there it would read plang's own environment.</summary>
    private static readonly string[] Base = ["/usr", "/lib", "/lib64", "/bin", "/sbin", "/etc", "/sys", "/dev"];
    /// <summary>Written by most programs, holding nothing: the one system path a program may write.</summary>
    private const string Sink = "/dev/null";

    /// <summary>A folder (or file) and the kernel rights its permission gives.</summary>
    internal readonly record struct Rule(string Path, ulong Rights);

    /// <summary>The program file: read and run, nothing else of its folder.</summary>
    public string Program { get; }
    public IReadOnlyList<Rule> Rules { get; }

    private Sandbox(string program, IReadOnlyList<Rule> rules)
    {
        Program = program;
        Rules = rules;
    }

    /// <summary>The hold for <paramref name="program"/> under <paramref name="permissions"/> — each one the caller's to
    /// grant, each an exact path with the verbs it gives; or why not.</summary>
    internal static async Task<(Sandbox? held, data.@this? refused)> Of(IEnumerable<data.@this> permissions, FilePath program,
        actor.context.@this context)
    {
        if (Unavailable() is { } why)
            return (null, context.Error(new global::app.error.ActionError(why, "PermissionNotEnforced", 501)));
        var caller = context.Actor?.Name ?? "";
        var rules = new List<Rule>();
        foreach (var row in permissions)
        {
            if (await row.Value<Permission>() is not { } permission)
                return (null, row.Error != null ? context.Error(row.Error) : Invalid($"{row.Name} is no permission — {{path, verbs}}"));
            if (permission.Match != global::app.type.item.permission.Match.Exact)
                return (null, Invalid($"{permission.Path} is a {permission.Match.ToString().ToLowerInvariant()}: the kernel holds a program to folders, named exactly"));
            // the actor is the caller until a program is an actor of its own (the Service actor)
            if (permission.Actor is { Length: > 0 } named && !string.Equals(named, caller, StringComparison.OrdinalIgnoreCase))
                return (null, Invalid($"a permission for {named}: a program {caller} starts is given {caller}'s own"));
            // a permission read as empty names nothing to hold (an empty path would be the app's root)
            if (string.IsNullOrWhiteSpace(permission.Path))
                return (null, Invalid("a permission names no path"));
            if (permission.Verbs.Count == 0)
                return (null, Invalid($"the permission for {permission.Path} names no verbs — read, write, delete"));
            if (permission.Verbs.Contains(Verb.Execute))
                return (null, Invalid($"execute ({permission.Path}) is not given to a program yet — read, write, delete"));
            var carrier = new data.@this("path", permission.Path, context: context);
            if (PathItem.Create(permission.Path, null, carrier) is not { } path)
                return (null, carrier.Error != null ? context.Error(carrier.Error) : Invalid($"{permission.Path} is no path"));
            ulong rights = 0;
            foreach (var verb in permission.Verbs)
            {
                var allowed = await path.Authorize(verb, context);
                if (allowed.Exits || !allowed.Success) return (null, allowed);
                rights |= Landlock.Rights(verb);
            }
            rules.Add(new Rule(path.Absolute, rights));
        }
        return (new Sandbox(program.Absolute, rules), null);

        data.@this Invalid(string message) => context.Error(new global::app.error.ActionError(message, "PermissionInvalid", 400));
    }

    /// <summary>Why a program can't be held here, or null when it can.</summary>
    public static string? Unavailable()
    {
        if (!OperatingSystem.IsLinux()) return "A program's permissions are held on Linux only.";
        var abi = Landlock.Version();
        return abi < 3 ? $"This kernel can't hold a program to its permissions (Landlock {(abi < 0 ? "is off" : $"version {abi}, needs 3")})." : null;
    }

    /// <summary>Starts the program held to its permissions. Throws when the lock can't be made: it never runs unlocked.</summary>
    public System.Diagnostics.Process Start(System.Diagnostics.ProcessStartInfo info)
    {
        // what the thread started, or why it couldn't — set once, by the thread
        var started = new TaskCompletionSource<System.Diagnostics.Process>();
        var thread = new Thread(() =>
        {
            try
            {
                Landlock.Restrict(Base, Sink, Program, Rules);
                started.SetResult(System.Diagnostics.Process.Start(info)
                    ?? throw new InvalidOperationException($"Could not start {info.FileName}"));
            }
            catch (Exception ex) { started.SetException(ex); }
        }) { IsBackground = true, Name = "permission" };
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

        private const ulong Execute = 1, WriteFile = 2, ReadFile = 4, ReadDir = 8, RemoveDir = 16, RemoveFile = 32,
            MakeChar = 64, MakeDir = 128, MakeReg = 256, MakeSock = 512, MakeFifo = 1024, MakeBlock = 2048,
            MakeSym = 4096, Refer = 8192, Truncate = 16384;
        private const ulong Reading = ReadFile | ReadDir;
        private const ulong Removing = RemoveFile | RemoveDir;
        // write: write, make files and folders, truncate, remove (a lock file replaced, git's), and
        // + make symlink: a checkout of a repo that holds a link makes one (git, tar). Safe: the kernel checks the path a
        //   link leads to when it is opened, so a link can't lead out of what the program holds
        // + refer: a move or link from one folder to another (git mv d1/y d2/y; without it EXDEV, "Invalid cross-device
        //   link"). Safe: the kernel moves a file only where the program holds at least the same rights
        private const ulong Writing = WriteFile | MakeDir | MakeReg | Truncate | Removing | MakeSym | Refer;
        // every right Landlock 3 guards — what no rule names is refused
        private const ulong Guarded = Execute | Reading | Writing | MakeChar | MakeSock | MakeFifo | MakeBlock;
        // what a file (not a folder) can be given: execute, write, read, truncate
        private const ulong FileRights = Execute | WriteFile | ReadFile | Truncate;

        /// <summary>The rights a verb gives.</summary>
        public static ulong Rights(Verb verb) => verb switch
        {
            Verb.Read => Reading,
            Verb.Write => Writing,
            Verb.Delete => Removing,
            _ => 0,
        };

        public static int Version() => (int)syscall(Create, IntPtr.Zero, 0, 1);

        /// <summary>Locks the calling thread: <paramref name="system"/> read and run where this system has it,
        /// <paramref name="sink"/> written, the program file read and run, each rule's path its rights — a rule's path
        /// that can't be held fails the lock.</summary>
        public static void Restrict(IEnumerable<string> system, string sink, string program, IEnumerable<Rule> rules)
        {
            ulong guarded = Guarded;
            var ruleset = (int)syscall(Create, ref guarded, sizeof(ulong), 0);
            if (ruleset < 0) throw new InvalidOperationException($"Landlock refused a ruleset (errno {Marshal.GetLastPInvokeError()})");
            try
            {
                foreach (var path in system) Allow(ruleset, path, Reading | Execute, required: false);
                Allow(ruleset, sink, ReadFile | WriteFile, required: false);
                Allow(ruleset, program, ReadFile | Execute, required: true);
                foreach (var rule in rules) Allow(ruleset, rule.Path, rule.Rights, required: true);
                if (prctl(NoNewPrivileges, 1, 0, 0, 0) != 0) throw new InvalidOperationException("no_new_privs was refused");
                if (syscall(RestrictSelf, ruleset, 0) < 0) throw new InvalidOperationException($"Landlock refused the lock (errno {Marshal.GetLastPInvokeError()})");
            }
            finally { close(ruleset); }
        }

        private const int NoSuchFile = 2, Invalid = 22;

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
                if (Marshal.GetLastPInvokeError() == Invalid && (access & FileRights) != 0)
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
