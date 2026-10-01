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
    /// <summary>The shortcut <paramref name="goal"/> is, named by its file; <paramref name="system"/> when it is
    /// one of <c>/system/shortcut/</c>, whose names an app may not take.</summary>
    internal @this(global::app.goal.@this goal, bool system)
    {
        Goal = goal;
        IsSystem = system;
    }

    /// <summary>Its name — its goal's file (<c>goal.goal</c> → <c>goal</c>), read as <c>%!goal%</c>.</summary>
    [global::app.Out]
    public string Name => Goal.Path?.FileNameWithoutExtension ?? Goal.Name;

    /// <summary>The goal a read starts.</summary>
    public global::app.goal.@this Goal { get; }

    /// <summary>One of <c>/system/shortcut/</c>: its name is sealed.</summary>
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
    /// the read.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this> Read(global::app.actor.context.@this asker)
    {
        await using (asker.Variable.Calls.Isolate(null))
            return await Goal.Start(asker);
    }

    public override string ToString() => Name;
}
