namespace app.shortcut.list;

/// <summary>
/// The app's shortcuts — <c>%!app.shortcut.list%</c>. Read once, at the first ask: each goal under
/// <c>/system/shortcut/</c> first — the app's own copy before the os's, its name sealed — then each under the app's
/// own <c>/shortcut/</c>, which adds names. Each is its file: its goal is read when it is read, so one that doesn't
/// load answers its own error and the others answer as they are. An app goal there that claims a system name is
/// refused: the read answers why (and how to override it), and so does every ask after it. A shortcut goal built after the read is not one
/// until the app starts again.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.shortcut.@this>
{
    private readonly global::app.@this _app;
    private readonly System.Lazy<System.Threading.Tasks.Task<global::app.data.@this>> _read;

    public @this(global::app.@this app) : base(new List<object?>(), app.type.list[typeof(@this)])
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
        var system = (await Names(global::app.type.item.path.@this.Resolve("/system/shortcut", context)))
            .Union(await Names(_app.filesystem.os.Combine("system").Combine("shortcut")), global::app.type.item.variable.list.@this.Comparer);
        // each a shortcut of its .goal file; its goal is read when the shortcut is read
        foreach (var name in system)
            Add(new global::app.shortcut.@this(global::app.type.item.path.@this.Resolve("/system/shortcut/" + name + ".goal", context), Shortcut));
        foreach (var name in await Names(global::app.type.item.path.@this.Resolve("/shortcut", context)))
        {
            var shortcut = new global::app.shortcut.@this(global::app.type.item.path.@this.Resolve("/shortcut/" + name + ".goal", context), Shortcut);
            if (Items().FirstOrDefault(s => s.IsSystem && global::app.type.item.variable.list.@this.Comparer.Equals(s.Name, shortcut.Name)) is { } taken)
                return context.Error(new global::app.error.@this(
                    $"{shortcut.Path} names %!{shortcut.Name}%, a system shortcut ({taken.Path}) — to change it, write " +
                    $"/system/shortcut/{shortcut.Name}.goal; to add one, name it otherwise",
                    "ShortcutCollision", 400));
            Add(shortcut);
        }
        return context.Ok(this);
    }

    // The type each shortcut is born holding: the app's shortcut.
    private global::app.type.@this Shortcut => _app.type.list[typeof(global::app.shortcut.@this)];

    // The goal names (.goal files, no extension) in `folder`; none when the folder isn't there.
    private async System.Threading.Tasks.Task<List<string>> Names(global::app.type.item.path.@this at)
    {
        var context = _app.actor.list.System.Context;
        if (!await (await at.Exists(context)).ToBooleanAsync()) return [];
        var listed = await at.List("*.goal", recursive: false, context);
        if (!listed.Success || await listed.Value() is not { } files) return [];
        return files.Items().Select(f => f.Stem.ToString()).ToList();
    }
}
