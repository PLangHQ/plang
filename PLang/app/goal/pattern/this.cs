namespace app.goal.pattern;

/// <summary>
/// The goals a glob names — <c>%!app.goal["/api/*"]%</c>, what the goal list answers for a name with a wildcard: never
/// the goals that match today, so a goal loaded later under <c>/api/</c> is named too. An event bound on it runs for each
/// goal it names (each goal answers whether it is, <see cref="global::app.type.item.@this.Is(@this)"/>). It has no steps,
/// and it never runs: its path is the glob.
/// </summary>
public sealed class @this : global::app.goal.@this
{
    private readonly global::app.type.item.path.Glob _glob;

    /// <summary>The goals <paramref name="glob"/> names, by their paths from the app's root (<c>/api/*</c>; a glob
    /// written without its leading slash is from the root too).</summary>
    internal @this(string glob, global::app.type.list.@this types, global::app.actor.context.@this context) : base(types)
    {
        var rooted = glob.StartsWith('/') ? glob : "/" + glob;
        Name = rooted;
        Path = global::app.type.item.path.@this.Resolve(rooted, context);
        _glob = new(rooted, global::app.type.item.path.@this.RootComparison);
    }

    /// <summary>Whether <paramref name="path"/>, a goal's, is one this glob names: its path from the root, with or
    /// without its <c>.goal</c>.</summary>
    internal bool Matches(global::app.type.item.path.@this? path)
    {
        if (path == null) return false;
        var raw = path.Raw;
        return _glob.Matches(raw) || (raw.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) && _glob.Matches(raw[..^5]));
    }

    /// <summary>An event bound on a pattern runs for each goal it names.</summary>
    internal override bool Has(global::app.type.item.@this item) => item.Is(this);

    /// <summary>A pattern stands for each goal it names.</summary>
    internal override bool IsMany => true;

    /// <summary>A pattern names goals; it doesn't run.</summary>
    public override Task<global::app.data.@this> Start(global::app.actor.context.@this context, IEnumerable<global::app.data.@this>? parameters)
        => Task.FromResult(context.Error(new global::app.error.@this(
            $"'{Name}' is a pattern of goals: it names them, it doesn't run", "NotAGoal", 400)));
}
