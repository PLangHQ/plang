namespace app.module.test;

/// <summary>
/// Declares tags for a test goal. The tags are the goal's own build-birth fact: <see cref="Build"/>
/// stamps them on the goal being built (<c>goal.Tag</c>), which writes them into its .pr; the test
/// born from that goal reads them there. Running the step does nothing, so a shared goal carrying a
/// tag step runs the same in and out of test mode.
/// </summary>
[Action("tag")]
public partial class Tag : IContext
{
    public partial data.@this<global::app.type.item.list.@this> Tags { get; init; }

    /// <summary>Stamps the literal tags on the goal being built — the goal whose frame its steps are checked in. A
    /// %variable% is only known at run, so it has nothing to stamp.</summary>
    public async Task<data.@this> Build()
    {
        if (Tags == null || Tags.HasVariable) return Context.Ok();
        if (Context.call.Current?.Goal is not { } goal) return Context.Ok();
        if (await Tags.Value() is not { } tags) return Context.Ok();

        var type = Context.App.type.list[typeof(global::app.type.item.tag.@this)];
        foreach (var row in tags.Items(Context))
            if (global::app.type.item.tag.@this.Create(await row.Value(), type) is { } tag && !goal.Tag.Has(tag))
                goal.Tag.Add(tag);
        return Context.Ok();
    }

    public Task<data.@this> Start() => Task.FromResult(Context.Ok());
}
