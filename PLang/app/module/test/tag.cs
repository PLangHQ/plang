using app.variable;

namespace app.module.test;

/// <summary>
/// Declares user tags for the running test. At discovery time, test.discover scans
/// the .pr for these actions to build the test's tag set — the runtime path is a
/// thin accumulator on the currently-running global::app.tester.Run (if any). Outside test mode
/// (Testing.CurrentTest == null, e.g. when a shared goal is reused in production),
/// the action no-ops rather than erroring so shared goals work in both modes.
/// </summary>
[Action("tag")]
public partial class Tag : IContext
{
    public partial data.@this<global::app.type.list.@this> Tags { get; init; }

    public async Task<data.@this> Run()
    {
        var currentTest = Context.App.Tester.CurrentTest;
        if (currentTest != null
            && (Tags == null ? null : global::app.type.item.@this.Lower<string[]>(await Tags.Value())) is { } tags)
        {
            foreach (var tag in tags)
                if (!string.IsNullOrWhiteSpace(tag))
                    currentTest.UserTags.Add(tag);
        }
        // Return the current tag set (empty list outside test mode) so plang tests
        // can inspect accumulated tags via %!data%.
        var snapshot = currentTest != null
            ? currentTest.UserTags.ToList()
            : new List<string>();
        return Context.Ok(snapshot);
    }
}
