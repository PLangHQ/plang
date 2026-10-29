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

    /// <summary>The goals <paramref name="glob"/> names, rooted as a plang path is: <c>/api/*</c> from the app's root,
    /// <c>api/*</c> in <paramref name="folder"/>, the folder of the goal whose step names it (the root when none does).</summary>
    internal @this(string glob, global::app.type.item.path.@this? folder, global::app.type.list.@this types,
        global::app.actor.context.@this context) : base(types)
    {
        Path = !glob.StartsWith('/') && folder is { } at ? at.Combine(glob)
            : global::app.type.item.path.@this.Resolve(glob.StartsWith('/') ? glob : "/" + glob, context);
        // its name, and the glob the matcher reads, are the path as written
        Name = Path.Raw;
        _glob = new(Path.Raw, global::app.type.item.path.@this.RootComparison);
    }

    /// <summary>Whether <paramref name="address"/>, a goal's, is one this glob names.</summary>
    internal bool Matches(string? address) => address != null && _glob.Matches(address);

    /// <summary>An event bound on a pattern runs for each goal it names.</summary>
    internal override bool Has(global::app.type.item.@this item) => item.Is(this);

    /// <summary>A pattern stands for each goal it names.</summary>
    internal override bool IsMany => true;

    /// <summary>A pattern names goals; it doesn't run.</summary>
    public override Task<global::app.data.@this> Start(global::app.actor.context.@this context, IEnumerable<global::app.data.@this>? parameters)
        => Task.FromResult(context.Error(new global::app.error.@this(
            $"'{Name}' is a pattern of goals: it names them, it doesn't run", "NotAGoal", 400)));
}
