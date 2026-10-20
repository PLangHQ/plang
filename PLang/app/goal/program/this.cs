namespace app.goal.program;

/// <summary>
/// The goals read once for every app made from the same code over the same folder — a program is the same for every
/// run of it, so a server making an app per request reads and parses each .pr once, not once per app. A goal is read by
/// the folder's own reader, an app of its own that runs nothing, so no app that asked keeps another alive; each app
/// holds the goal in its own list and its own load events run on it. A goal read is kept while its file is the one read
/// (its modified time and size); a changed file is read again. An app building, or reading through a file system of
/// its own (a build's overlay), reads its own.
/// </summary>
internal static class @this
{
    private sealed record Held(System.DateTime? Modified, long Size, global::app.goal.@this Goal);

    // the folder's reader, one per code and folder
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(global::app.assembly.@this Code, string Root),
        System.Lazy<global::app.@this>> Readers = new();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(global::app.assembly.@this Code, string Root, string Pr),
        Held> Goals = new();

    /// <summary>The goal <paramref name="pr"/> holds, read once for <paramref name="app"/>'s code and folder; null when
    /// the app reads its own (it is building, its files are its own), and the read's failure when the file can't be read.</summary>
    internal static async System.Threading.Tasks.Task<global::app.data.@this?> Read(global::app.type.item.path.@this pr, global::app.@this app)
    {
        var context = app.actor.list.System.Context;
        if (app.Build != null || !ReferenceEquals(context.FileSystem, app.FileSystem)) return null;

        var stat = await pr.Stat(context);
        if (!stat.Success || await stat.Value() is not { Exists: true } found) return null;
        var code = app.type.list.Code;
        var key = (code, app.AbsolutePath, pr.ToString());
        var size = found.Size?.Value ?? -1;
        if (Goals.TryGetValue(key, out var held) && held.Modified == found.Modified && held.Size == size)
            return context.Ok(held.Goal);

        var reader = Readers.GetOrAdd((code, app.AbsolutePath), static at => new(
            () => new global::app.@this(at.Root, autoWireConsoleChannels: false, assembly: at.Code),
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        var asker = reader.actor.list.System.Context;
        var read = await pr.Read(asker);
        var content = read.Success ? await read.Value() : null;
        if (content is not global::app.goal.@this goal) return null;
        Goals[key] = new Held(found.Modified, size, goal);
        return context.Ok(goal);
    }
}
