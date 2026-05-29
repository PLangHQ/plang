using app.actor.context;
using app.variable;
using app.modules.builder;
using Action = global::app.goal.steps.step.actions.action.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.mergeStep action — thin delegation to Step.Merge().
/// </summary>
public class MergeStepTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_mergestep_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
        _app.Builder.IsEnabled = true;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }
    }

    [Test]
    public async Task MergeStep_DelegatesToStepMerge()
    {
        var target = new Step { Text = "do something", Index = 0, LineNumber = 1 };
        var source = new Step
        {
            Text = "do something",
            Actions = new StepActions(new[]
            {
                new Action { Module = "output", ActionName = "write", Parameters = new List<Data> { new("Message", "hi") } }
            })
        };

        var action = new merge { Context = _app.User.Context, Step = target, StepFromLlm = source };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var merged = result.Value as Step;
        await Assert.That(merged).IsNotNull();
        await Assert.That(merged!.Actions.Count).IsEqualTo(1);
        await Assert.That(merged.Actions[0].Module).IsEqualTo("output");
        // Structural fields preserved
        await Assert.That(merged.Text).IsEqualTo("do something");
        await Assert.That(merged.Index).IsEqualTo(0);
    }
}
