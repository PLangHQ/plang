namespace app.shortcut.list;

/// <summary>
/// The app's shortcuts — <c>%!app.shortcut.list%</c>. Read once, at the first ask: each goal under
/// <c>/system/shortcut/</c> first — the app's own copy before the os's, its name sealed — then each under the app's
/// own <c>/shortcut/</c>, which adds names. An app goal there that claims a system name is refused: the read answers
/// why (and how to override it), and so does every ask after it. A shortcut goal built after the read is not one
/// until the app starts again.
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
        // the system's names, the app's /system/shortcut/ and the os's; each found as the goal list finds it
        var system = (await Names("/system/shortcut"))
            .Union(await Names(_app.OsAbsolutePath + "/system/shortcut"), global::app.type.item.variable.list.@this.Comparer);
        foreach (var name in system)
            if (await Goal("/system/shortcut/" + name) is { } goal) Add(new global::app.shortcut.@this(goal));
        foreach (var name in await Names("/shortcut"))
        {
            if (await Goal("/shortcut/" + name) is not { } goal) continue;
            var shortcut = new global::app.shortcut.@this(goal);
            if (Items().FirstOrDefault(s => s.IsSystem && global::app.type.item.variable.list.@this.Comparer.Equals(s.Name, shortcut.Name)) is { } taken)
                return context.Error(new global::app.error.Error(
                    $"{goal.Path} names %!{shortcut.Name}%, a system shortcut ({taken.Goal.Path}) — to change it, write " +
                    $"/system/shortcut/{shortcut.Name}.goal; to add one, name it otherwise",
                    "ShortcutCollision", 400));
            Add(shortcut);
        }
        return context.Ok(this);
    }

    // The goal names (.goal files, no extension) in `folder`; none when the folder isn't there.
    private async System.Threading.Tasks.Task<List<string>> Names(string folder)
    {
        var context = _app.actor.list.System.Context;
        var at = global::app.type.item.path.@this.Resolve(folder, context);
        if (!await (await at.Exists(context)).ToBooleanAsync()) return [];
        var listed = await at.List("*.goal", recursive: false, context);
        if (!listed.Success || await listed.Value() is not { } files) return [];
        return files.Items().Select(f => f.FileNameWithoutExtension).ToList();
    }

    // The goal the address names, found as a call finds it; one that doesn't load is left out, said on debug.
    private async System.Threading.Tasks.Task<global::app.goal.@this?> Goal(string address)
    {
        var found = await _app.goal.list.Find(address);
        if (found.Peek() is global::app.goal.@this goal) return goal;
        await (_app.Debug?.Write($"shortcut: {address} left out — {found.Error?.Message ?? "not found"}") ?? System.Threading.Tasks.Task.CompletedTask);
        return null;
    }
}
