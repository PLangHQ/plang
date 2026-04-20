using App.Variables;

namespace App.modules.test;

/// <summary>
/// Declares user tags for the running test. At discovery time, test.discover scans
/// the .pr for these actions to build the test's tag set — the runtime path is a
/// thin accumulator on the currently-running TestRun (if any). Outside test mode
/// (Testing.CurrentTest == null, e.g. when a shared goal is reused in production),
/// the action no-ops rather than erroring so shared goals work in both modes.
/// </summary>
[Example("set test tag 'http', 'fast'", "Tags=[http, fast]")]
[Example("set test tag 'slow'", "Tags=[slow]")]
[Action("tag")]
public partial class Tag : IContext
{
    public partial Data.@this<string[]> Tags { get; init; }

    public Task<Data.@this> Run()
    {
        var currentTest = Context.App!.Testing.CurrentTest;
        if (currentTest != null && Tags.Value is { } tags)
        {
            foreach (var tag in tags)
                if (!string.IsNullOrWhiteSpace(tag))
                    currentTest.UserTags.Add(tag);
        }
        return Task.FromResult(App.Data.@this.Ok());
    }
}
