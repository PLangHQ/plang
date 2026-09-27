namespace app.module.action.test;

/// <summary>
/// Runs the tests given — the run is the app's test list's (<c>app.test.list.Start</c>): each test in an
/// App of its own, as many at once and under the timeout test's setting says (<c>%!app.test.setting%</c>).
/// A test's failure is its outcome, never the run's. Returns the tests, each carrying its outcome.
/// </summary>
[Action("start", Cacheable = false)]
public partial class start : IContext
{
    [IsNotNull]
    public partial data.@this<global::app.type.item.list.@this<global::app.test.@this>> Tests { get; init; }

    public async Task<data.@this<global::app.type.item.list.@this<global::app.test.@this>>> Start()
        => Context.Ok(await Context.App.test.list.Start((await Tests.Value())!, Context));
}
