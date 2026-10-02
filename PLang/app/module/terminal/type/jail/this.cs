using Data = global::app.data.@this;
using PathItem = global::app.type.item.path.@this;

namespace app.module.terminal.type.jail;

/// <summary>
/// PLang <c>jail</c> value — the folders a program started by the terminal is held to: <c>{read: ["/src/os"],
/// write: ["/src/os"]}</c>. Given to <c>terminal.open</c> or <c>terminal.start</c> as <c>Jail</c>, the kernel (Linux
/// Landlock) holds the program there: it reads and runs only what <c>read</c> and <c>write</c> name, its own folder and
/// the system's libraries (/usr /lib /lib64 /bin /sbin /etc /sys /dev — not /proc, where it would read plang's own
/// environment), and writes only what <c>write</c> names (and /dev/null). Each folder is first the caller's to read or
/// write, so a program is never handed more than the app may touch itself. No jail, no hold. The terminal's own:
/// it is the one module that starts programs.
///
/// <para>Held for its life: a lock can only narrow, so a folder not named stays shut until the program is started
/// again with a wider jail.</para>
///
/// <para>What it does not hold yet: the program runs as plang's own user, so it can signal plang (Landlock's scoping
/// of signals comes with Linux 6.12; WSL has 6.6), and it reaches the network freely (an os proxy is to come, as
/// <c>network</c>). Folders hidden inside a granted one (<c>hide</c>) are to come too.</para>
/// </summary>
[global::app.Attributes.PlangType("jail")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{read: [\"/src/os\"], write: [\"/src/os/.git\"]}";
    public static string Description => "The folders a started program is held to by the kernel: what it may read, what it may write — nothing else.";
    public static string Shape => "object";

    /// <summary>The folders the program may read (and run from) — a folder or a list of them, as written.</summary>
    [Out, Store] public Data? Read { get; }

    /// <summary>The folders the program may write (and read) — a folder or a list of them, as written.</summary>
    [Out, Store] public Data? Write { get; }

    public @this() : this(null, null) { }

    internal @this(Data? read, Data? write)
    {
        Read = read;
        Write = write;
    }

    public override bool IsLeaf => false;

    /// <summary>A jail is made from a dict of its members — <c>read</c> and <c>write</c>, each a folder or a list of
    /// them, kept as written (each becomes a path when the jail starts a program, with the caller's context); a member
    /// that is no member of a jail declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this jail) return jail;
        // none named: no jail — the program runs as any other
        if (raw is null or global::app.type.item.@null.@this) return null;
        if (raw is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a jail is {read: [folders], write: [folders]}", "JailInvalid", 400));
            return null;
        }
        Data? read = null, write = null;
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "read": read = entry; break;
                case "write": write = entry; break;
                default:
                    data.Fail(new global::app.error.Error($"a jail's members are read and write (folders) — not {entry.Name}", "JailInvalid", 400));
                    return null;
            }
        return new @this(read, write);
    }

    /// <summary>Writes itself as its dict, each member as written.</summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("read"); await Member(Read);
        writer.Name("write"); await Member(Write);
        writer.EndObject();

        async System.Threading.Tasks.ValueTask Member(Data? member)
        {
            if (member == null || await member.Value() is not { } value) { writer.BeginArray(0); writer.EndArray(); return; }
            await value.Output(writer, mode, context);
        }
    }

    /// <summary>The folders a member names, each a path — or why one is no path.</summary>
    private static async Task<(List<PathItem> folders, Data? refused)> Folders(Data? member, global::app.actor.context.@this context)
    {
        var folders = new List<PathItem>();
        if (member == null || await member.Value() is not { } value) return (folders, null);
        // a list's folders are its rows, each decoded through its own Data; one folder is itself
        var named = value is global::app.type.item.list.@this list ? list.Rows(context).ToList() : [member];
        foreach (var row in named)
        {
            var folder = await row.Value();
            var carrier = new Data("folder", folder, context: context);
            if (PathItem.Create(folder, null, carrier) is not { } path)
                return (folders, carrier.Error != null ? context.Error(carrier.Error) : context.Error(
                    new global::app.error.Error($"{folder} is no folder", "JailInvalid", 400)));
            folders.Add(path);
        }
        return (folders, null);
    }

    /// <summary>Starts the program inside this jail — the running program, or why it isn't. Each folder is first the
    /// caller's to read or write (asked on the caller's channel when it isn't yet); where the kernel can't hold the
    /// program, or a folder can't be held, it isn't started: a program asked to be held never runs free.</summary>
    internal async Task<data.@this<Process>> Start(System.Diagnostics.ProcessStartInfo info, global::app.actor.context.@this context)
    {
        var (hold, refused) = await Hold(context);
        if (refused != null) return data.@this<Process>.From(refused);
        try
        {
            var os = hold!.Start(info);
            return context.Ok<Process>(new Process { Program = info.FileName, Id = os.Id, Os = os });
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            return data.@this<Process>.From(context.Error(new global::app.error.ActionError(
                $"Could not hold {info.FileName} to its folders: {ex.Message}", "JailFailed", 500)));
        }
    }

    /// <summary>The kernel's hold for this jail — each folder first the caller's to read or write; or why not.</summary>
    private async Task<(code.Jail? hold, Data? refused)> Hold(global::app.actor.context.@this context)
    {
        if (code.Jail.Unavailable() is { } why) return (null, context.Error(new global::app.error.ActionError(why, "JailUnavailable", 501)));
        async Task<(List<string> folders, Data? refused)> Granted(Data? member, global::app.type.item.permission.Verb verb)
        {
            var folders = new List<string>();
            var (paths, notPaths) = await Folders(member, context);
            if (notPaths != null) return (folders, notPaths);
            foreach (var path in paths)
            {
                var allowed = await path.Authorize(verb, context);
                if (allowed.Exits || !allowed.Success) return (folders, allowed);
                folders.Add(path.Absolute);
            }
            return (folders, null);
        }
        var (reading, notRead) = await Granted(Read, global::app.type.item.permission.Verb.Read);
        if (notRead != null) return (null, notRead);
        var (writing, notWritten) = await Granted(Write, global::app.type.item.permission.Verb.Write);
        if (notWritten != null) return (null, notWritten);
        return (new code.Jail(reading, writing), null);
    }
}
