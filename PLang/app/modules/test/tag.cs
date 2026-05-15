using app.Variables;

namespace app.modules.test;

/// <summary>
/// Declares user tags for the running test. At discovery time, test.discover scans
/// the .pr for these actions to build the test's tag set — the runtime path is a
/// thin accumulator on the currently-running global::app.Tester.Run (if any). Outside test mode
/// (Testing.CurrentTest == null, e.g. when a shared goal is reused in production),
/// the action no-ops rather than erroring so shared goals work in both modes.
/// </summary>
[System.ComponentModel.Description("Attach user-defined tags to the currently running test for filtering and reporting")]
[Example("tag this test 'http', 'fast'",
    "test.tag Tags([list<string>] [\"http\", \"fast\"])")]
[Action("tag")]
public partial class Tag : IContext
{
    public partial Data.@this<string[]> Tags { get; init; }

    public Task<Data.@this> Run()
    {
        var currentTest = Context.App!.Tester.CurrentTest;
        if (currentTest != null && Tags.Value is { } tags)
        {
            foreach (var tag in tags)
                if (!string.IsNullOrWhiteSpace(tag))
                    currentTest.UserTags.Add(tag);
        }
        // Return the current tag set (empty list outside test mode) so plang tests
        // can inspect accumulated tags via %__data__%.
        var snapshot = currentTest != null
            ? currentTest.UserTags.ToList()
            : new List<string>();
        return Task.FromResult(app.Data.@this.Ok(snapshot));
    }
}
