using app.variable;

namespace app.module.action.test;

/// <summary>
/// Declares user tags for the running test. At discovery time, test.discover scans
/// the .pr for these actions to build the test's tag set — the runtime path is a
/// thin accumulator on the currently-running test (if any). Outside test mode
/// (Test.Current == null, e.g. when a shared goal is reused in production),
/// the action no-ops rather than erroring so shared goals work in both modes.
/// </summary>
[Action("tag")]
public partial class Tag : IContext
{
    public partial data.@this<global::app.type.item.list.@this> Tags { get; init; }

    public async Task<data.@this> Run()
    {
        var currentTest = Context.App.Test.Current;
        // Move the tags into the running test as text — resolve the lazy param once
        // (the store boundary), then it's a direct read for every later membership check.
        if (currentTest != null && Tags != null && await Tags.Value() is { } incoming)
            currentTest.Tags.Add(incoming);

        // Return the current tag set (empty outside test mode) so plang tests can
        // inspect accumulated tags via %!data%.
        return Context.Ok(currentTest?.Tags
            ?? new global::app.type.item.list.@this<global::app.type.item.text.@this>(Context));
    }
}
