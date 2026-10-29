namespace app.shortcut;

/// <summary>
/// A name that reads as a goal's answer: <c>%!goal%</c> is what <c>/system/shortcut/goal.goal</c> returns for its
/// asker. Each goal under a <c>shortcut/</c> folder is one, named by its file. Reading it starts its goal in the
/// asker's context, under a call with memory of its own, so a read writes nothing the asker sees — its
/// <c>%!data%</c> stays as it was. A shortcut goal reads who asked through the stack
/// (<c>%!app.callstack.scope.caller.goal%</c>).
/// </summary>
[global::app.Attributes.PlangType("shortcut")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.IMatch<@this>, global::app.type.item.ILoad<@this>,
    global::app.type.item.IList<@this, list.@this>
{
    /// <summary>The shortcut the <c>.goal</c> file at <paramref name="path"/> is, named by its file — a value of the app's
    /// shortcut <paramref name="type"/>. Its goal is read when the shortcut is read.</summary>
    internal @this(global::app.type.item.path.@this path, global::app.type.@this type) : base(type)
    {
        Path = path;
        IsSystem = path.ToString().StartsWith("/system/shortcut/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Its name — its file's (<c>goal.goal</c> → <c>goal</c>), read as <c>%!goal%</c>.</summary>
    [global::app.Out]
    public string Name => Path.Stem.ToString();

    /// <summary>Its <c>.goal</c> file — the goal a read starts is the one built from it.</summary>
    public global::app.type.item.path.@this Path { get; }

    /// <summary>One of <c>/system/shortcut/</c> (the app's own copy or the os's): its name is sealed.</summary>
    internal bool IsSystem { get; }

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>A key names this shortcut by its name, compared as a variable's name is.</summary>
    public System.Threading.Tasks.ValueTask<@this?> Match(string key)
        => new(global::app.type.item.variable.list.@this.Comparer.Equals(key, Name) ? this : null);

    /// <summary>The app's shortcuts.</summary>
    public static list.@this List(global::app.@this app) => new(app);

    /// <summary>What it reads as for <paramref name="asker"/>: its goal's answer, started in the asker's context
    /// under a call of its own, so every write the goal makes (its return's <c>%!data%</c> among them) ends with
    /// the read. Its goal is loaded through the goal list, which keeps what it loaded; one that doesn't load is this
    /// read's answer, its load's own error, and nothing is kept of it — the next read loads again. The goal a read
    /// loaded is kept: every read after it starts it at once.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Read(global::app.actor.context.@this asker)
    {
        if (_goal is not { } goal)
        {
            var loaded = await asker.App.goal.list.Load(Path);
            if (loaded.Peek() is not global::app.goal.@this read) return loaded.Error != null ? loaded : asker.NotFound(Name);
            goal = _goal = read;
        }
        await using (asker.call.Isolate(null, await asker.call.Setting()))
            return await goal.Start(asker);
    }

    // the goal a read loaded, kept once it loads; none while it hasn't
    private global::app.goal.@this? _goal;

    public override string ToString() => Name;
}
