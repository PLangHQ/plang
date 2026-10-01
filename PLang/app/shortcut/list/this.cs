namespace app.shortcut.list;

/// <summary>
/// The app's shortcuts — <c>%!app.shortcut.list%</c>. Read once, at the first ask: each goal under
/// <c>/system/shortcut/</c> first, its name sealed, then each under the app's own <c>/shortcut/</c>, which adds
/// names. An app goal that claims a system name is refused: the read answers why, and so does every ask after it.
/// A shortcut goal built after the read is not one until the app starts again.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.shortcut.@this>
{
    private readonly global::app.@this _app;
    private readonly System.Lazy<System.Threading.Tasks.Task<global::app.data.@this>> _read;

    public @this(global::app.@this app) : base(new List<object?>())
    {
        _app = app;
        _read = new(() => Fill(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The shortcuts read from their folders — once; a clash with a system name is the answer.</summary>
    public System.Threading.Tasks.Task<global::app.data.@this> Read() => _read.Value;

    /// <summary>The shortcuts, read first if they aren't yet; a refused read throws its error.</summary>
    internal override async IAsyncEnumerable<global::app.shortcut.@this> Walk()
    {
        var read = await Read();
        if (!read.Success) throw new global::app.error.AppException(read.Error!);
        foreach (var shortcut in Items()) yield return shortcut;
    }

    private async System.Threading.Tasks.Task<global::app.data.@this> Fill()
    {
        var context = _app.actor.list.System.Context;
        foreach (var goal in await Goals(_app.OsAbsolutePath + "/system/shortcut/.build"))
            Add(new global::app.shortcut.@this(goal, system: true));
        foreach (var goal in await Goals("/shortcut/.build"))
        {
            var shortcut = new global::app.shortcut.@this(goal, system: false);
            if (Items().FirstOrDefault(s => s.IsSystem && global::app.type.item.variable.list.@this.Comparer.Equals(s.Name, shortcut.Name)) is { } taken)
                return context.Error(new global::app.error.Error(
                    $"{goal.Path} names the shortcut %!{shortcut.Name}%, which is the system's ({taken.Goal.Path}) — name it otherwise",
                    "ShortcutCollision", 400));
            Add(shortcut);
        }
        return context.Ok(this);
    }

    // The goals whose .pr files are in `build`, read as the app itself; none when the folder isn't there.
    private async System.Threading.Tasks.Task<List<global::app.goal.@this>> Goals(string build)
    {
        var context = _app.actor.list.System.Context;
        var folder = global::app.type.item.path.@this.Resolve(build, context);
        var exists = await folder.Exists(context);
        if (!exists.Success || !await exists.ToBooleanAsync()) return [];
        var listed = await folder.List("*.pr", recursive: false, context);
        if (!listed.Success || await listed.Value() is not { } files) return [];
        var goals = new List<global::app.goal.@this>();
        foreach (var pr in files.Items())
        {
            var loaded = await global::app.goal.@this.Load(pr, _app);
            if (loaded.Success && await loaded.Value() is global::app.goal.@this goal) goals.Add(goal);
            else await (_app.Debug?.Write($"shortcut: {pr} left out — {loaded.Error?.Message}") ?? System.Threading.Tasks.Task.CompletedTask);
        }
        return goals;
    }
}
