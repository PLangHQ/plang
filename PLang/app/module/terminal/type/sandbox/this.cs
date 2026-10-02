using Data = global::app.data.@this;
using PathItem = global::app.type.item.path.@this;
using Folders = global::app.type.item.list.@this<global::app.type.item.path.@this>;

namespace app.module.terminal.type.sandbox;

/// <summary>
/// PLang <c>sandbox</c> value — the folders a program started by the terminal is held to: <c>{read: ["/src/os"],
/// write: ["/src/os"]}</c>. Given to <c>terminal.open</c> or <c>terminal.start</c> as <c>Sandbox</c>, the kernel (Linux
/// Landlock) holds the program there: it reads only what <c>read</c> and <c>write</c> name, its own program file and
/// the system's libraries (/usr /lib /lib64 /bin /sbin /etc /sys /dev — not /proc, where it would read plang's own
/// environment), and writes only what <c>write</c> names (and /dev/null). Each folder is first the caller's to read or
/// write, so a program is never handed more than the app may touch itself. Left out, the program runs free; named, it
/// never does. The terminal's own: it is the one module that starts programs.
///
/// <para>Held for its life: a lock can only narrow, so a folder not named stays shut until the program is started
/// again in a wider sandbox.</para>
///
/// <para>In a write folder the program may also move files between folders and make symlinks (a checkout does both);
/// the kernel still holds what a link leads to, and moves a file only where it has at least the same rights.</para>
///
/// <para>No /tmp: a program that writes temporary files fails unless its sandbox names a folder for them (and the
/// program is told of it, e.g. <c>TMPDIR</c> in its Environment).</para>
///
/// <para>What it does not hold yet: the program runs as plang's own user, so it can signal plang (Landlock's scoping
/// of signals comes with Linux 6.12; WSL has 6.6), and it reaches the network freely (an os proxy is to come, as
/// <c>network</c>). Folders hidden inside a granted one (<c>hide</c>) are to come too.</para>
/// </summary>
[global::app.Attributes.PlangType("sandbox")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{\"read\": [\"/src/os\"], \"write\": [\"/src/os/.git\"]}";
    public static string Description => "The folders a started program is held to by the kernel: what it may read, what it may write — nothing else.";
    public static string Shape => "object";

    // The folders the program may read, and may write (and read) — %sandbox.read%, %sandbox.write% (Get). Named apart
    // in C#: an item's own Read and Write are its serializer's doors.
    internal data.@this<Folders>? Reading { get; }
    internal data.@this<Folders>? Writing { get; }

    public @this() : this(null, null) { }

    internal @this(data.@this<Folders>? read, data.@this<Folders>? write)
    {
        Reading = read;
        Writing = write;
    }

    public override bool IsLeaf => false;

    /// <summary>One step down: <c>.read</c> or <c>.write</c>, the folders as written (none named, an empty list).</summary>
    public override System.Threading.Tasks.ValueTask<Data> Get(Data parent, string key)
        => key.ToLowerInvariant() switch
        {
            "read" => new(Reading is { } read ? read : new Data(key, new Folders(), parent: parent)),
            "write" => new(Writing is { } write ? write : new Data(key, new Folders(), parent: parent)),
            _ => base.Get(parent, key),
        };

    /// <summary>A sandbox is made from a dict of its members — <c>read</c> and <c>write</c>, each a list of folders (each
    /// a path when the sandbox starts a program, with the caller's context); a member that is no member of a sandbox
    /// declines with why. Nothing makes no sandbox — the step decides whether that is allowed.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this sandbox) return sandbox;
        if (raw is null or global::app.type.item.@null.@this) return null;
        if (raw is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a sandbox is {read: [folders], write: [folders]}", "SandboxInvalid", 400));
            return null;
        }
        data.@this<Folders>? read = null, write = null;
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "read": read = global::app.data.@this<Folders>.From(entry); break;
                case "write": write = global::app.data.@this<Folders>.From(entry); break;
                default:
                    data.Fail(new global::app.error.Error($"a sandbox's members are read and write (lists of folders) — not {entry.Name}", "SandboxInvalid", 400));
                    return null;
            }
        return new @this(read, write);
    }

    /// <summary>Writes itself as its dict, each member its list of folders.</summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("read"); await Member(Reading);
        writer.Name("write"); await Member(Writing);
        writer.EndObject();

        async System.Threading.Tasks.ValueTask Member(data.@this<Folders>? member)
        {
            if (member == null || await member.Value() is not { } folders) { writer.BeginArray(0); writer.EndArray(); return; }
            await folders.Output(writer, mode, context);
        }
    }

    /// <summary>Starts the program inside this sandbox — the running program, or why it isn't. Each folder is first the
    /// caller's to read or write (asked on the caller's channel when it isn't yet); the program file is the one the
    /// caller may already run. Where the kernel can't hold the program, or a folder can't be held, it isn't started:
    /// a program asked to be held never runs free.</summary>
    internal async Task<data.@this<Process>> Start(System.Diagnostics.ProcessStartInfo info, PathItem program, global::app.actor.context.@this context)
    {
        var (hold, refused) = await Hold(program, context);
        if (refused != null) return data.@this<Process>.From(refused);
        try
        {
            var os = hold!.Start(info);
            return context.Ok<Process>(new Process { Program = program.Absolute, Id = os.Id, Os = os });
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
        {
            return data.@this<Process>.From(context.Error(new global::app.error.ActionError(
                $"Could not hold {program.Absolute} in its sandbox: {ex.Message}", "SandboxFailed", 500)));
        }
    }

    /// <summary>The kernel's hold for this sandbox — each folder first the caller's to read or write; or why not.</summary>
    private async Task<(code.Sandbox? hold, Data? refused)> Hold(PathItem program, global::app.actor.context.@this context)
    {
        if (code.Sandbox.Unavailable() is { } why) return (null, context.Error(new global::app.error.ActionError(why, "SandboxUnavailable", 501)));
        var (reading, notRead) = await Granted(Reading, global::app.type.item.permission.Verb.Read, context);
        if (notRead != null) return (null, notRead);
        var (writing, notWritten) = await Granted(Writing, global::app.type.item.permission.Verb.Write, context);
        if (notWritten != null) return (null, notWritten);
        return (new code.Sandbox(program.Absolute, reading, writing), null);
    }

    /// <summary>The folders a member names, each one the caller's to <paramref name="verb"/> — or why not.</summary>
    private static async Task<(List<string> folders, Data? refused)> Granted(data.@this<Folders>? member,
        global::app.type.item.permission.Verb verb, global::app.actor.context.@this context)
    {
        var granted = new List<string>();
        if (member == null) return (granted, null);
        if (await member.Value() is not { } folders)
            return (granted, member.Error != null ? context.Error(member.Error)
                : context.Error(new global::app.error.Error($"a sandbox's {member.Name} is a list of folders", "SandboxInvalid", 400)));
        foreach (var row in folders.Rows(context))
        {
            if (await row.Value<PathItem>() is not { } path)
                return (granted, row.Error != null ? context.Error(row.Error)
                    : context.Error(new global::app.error.Error($"a sandbox's {member.Name} holds what is no folder", "SandboxInvalid", 400)));
            var allowed = await path.Authorize(verb, context);
            if (allowed.Exits || !allowed.Success) return (granted, allowed);
            granted.Add(path.Absolute);
        }
        return (granted, null);
    }
}
