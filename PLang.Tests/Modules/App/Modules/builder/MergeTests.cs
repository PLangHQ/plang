using app;
using app.goal;
using app.goal.step;
using app.variable;
using Action = global::app.goal.step.action.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for Step.Merge() and Goal.Merge() — OBP methods that own
/// the knowledge of which fields are LLM-derived vs structural.
/// </summary>
public class MergeTests
{
    #region Step.Merge

    [Test]
    public async Task StepMerge_CopiesActions()
    {
        var target = new Step { Text = "do something", Index = 0 };
        var source = new Step
        {
            Text = "do something",
            Action = new StepActions(new[]
            {
                new Action { Module = "output", ActionName = "write", Parameter = new List<Data> { new("Message", "hello", context: global::PLang.Tests.TestApp.SharedContext) } }
            })
        };

        target.Merge(source);

        await Assert.That(target.Action.Count).IsEqualTo(1);
        await Assert.That(target.Action[0].Module).IsEqualTo("output");
    }

    [Test]
    public async Task StepMerge_PreservesStructuralFields()
    {
        var target = new Step { Text = "original text", Index = 5, Indent = 2, LineNumber = 10 };
        var source = new Step
        {
            Text = "different text",
            Index = 99,
            Indent = 0,
            LineNumber = 1,
            Action = new StepActions(new[]
            {
                new Action { Module = "file", ActionName = "read" }
            })
        };

        target.Merge(source);

        // Structural fields preserved
        await Assert.That(target.Text).IsEqualTo("original text");
        await Assert.That(target.Index).IsEqualTo(5);
        await Assert.That(target.Indent).IsEqualTo(2);
        await Assert.That(target.LineNumber).IsEqualTo(10);
        // LLM field copied
        await Assert.That(target.Action.Count).IsEqualTo(1);
    }

    [Test]
    public async Task StepMerge_EmptySource_LeavesTargetUnchanged()
    {
        var originalAction = new Action { Module = "output", ActionName = "write" };
        var target = new Step
        {
            Text = "step",
            Action = new StepActions(new[] { originalAction })
        };
        var source = new Step { Text = "step" }; // Empty LLM fields

        target.Merge(source);

        // Actions not cleared because source has 0 actions
        await Assert.That(target.Action.Count).IsEqualTo(1);
    }

    [Test]
    public async Task StepMerge_WarningsReplacedOnlyWhenSourceHasEntries()
    {
        var target = new Step
        {
            Text = "step",
            Warning = { new global::app.warning.@this { Key = "W1", Message = "original warning" } }
        };

        // Source with no warnings — target keeps its warnings
        var emptySource = new Step { Text = "step" };
        target.Merge(emptySource);
        await Assert.That(target.Warning.Count).IsEqualTo(1);
        await Assert.That(target.Warning[0].Key).IsEqualTo("W1");

        // Source with warnings — target warnings replaced
        var sourceWithWarnings = new Step
        {
            Text = "step",
            Warning = { new global::app.warning.@this { Key = "W2", Message = "new warning" } }
        };
        target.Merge(sourceWithWarnings);
        await Assert.That(target.Warning.Count).IsEqualTo(1);
        await Assert.That(target.Warning[0].Key).IsEqualTo("W2");
    }

    #endregion

    #region Goal.Merge

    [Test]
    public async Task GoalMergeFrom_MatchesByText_MergesLlmFields()
    {
        var freshGoal = new Goal
        {
            Name = "Test",
            Step = new GoalSteps
            {
                new Step { Text = "do something", Index = 0 },
                new Step { Text = "do another thing", Index = 1 }
            }
        };

        var existingGoal = new Goal
        {
            Name = "Test",
            Step = new GoalSteps
            {
                new Step
                {
                    Text = "do something",
                    Action = new StepActions(new[]
                    {
                        new Action { Module = "output", ActionName = "write" }
                    })
                }
            }
        };

        freshGoal.Merge(existingGoal);

        // Matched step gets actions
        await Assert.That(freshGoal.Step[0].Action.Count).IsEqualTo(1);
        // Unmatched step keeps empty
        await Assert.That(freshGoal.Step[1].Action.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalMergeFrom_UnmatchedSteps_KeepEmptyActions()
    {
        var freshGoal = new Goal
        {
            Name = "Test",
            Step = new GoalSteps
            {
                new Step { Text = "new step that didn't exist before", Index = 0 }
            }
        };

        var existingGoal = new Goal
        {
            Name = "Test",
            Step = new GoalSteps
            {
                new Step
                {
                    Text = "old step text",
                    Action = new StepActions(new[]
                    {
                        new Action { Module = "file", ActionName = "read" }
                    })
                }
            }
        };

        freshGoal.Merge(existingGoal);

        await Assert.That(freshGoal.Step[0].Action.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalMergeFrom_NullExisting_NoOp()
    {
        var freshGoal = new Goal
        {
            Name = "Test",
            Step = new GoalSteps { new Step { Text = "step", Index = 0 } }
        };

        // Should not throw
        freshGoal.Merge(null);

        await Assert.That(freshGoal.Step.Count).IsEqualTo(1);
        await Assert.That(freshGoal.Step[0].Action.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalMergeFrom_DuplicateStepText_FirstMatchOnly()
    {
        // Two steps with identical text in fresh goal, one match in existing
        var freshGoal = new Goal
        {
            Name = "Test",
            Step = new GoalSteps
            {
                new Step { Text = "do something", Index = 0 },
                new Step { Text = "do something", Index = 1 }
            }
        };

        var existingGoal = new Goal
        {
            Name = "Test",
            Step = new GoalSteps
            {
                new Step
                {
                    Text = "do something",
                    Action = new StepActions(new[]
                    {
                        new Action { Module = "output", ActionName = "write" }
                    })
                }
            }
        };

        freshGoal.Merge(existingGoal);

        // First match gets the actions, second stays empty
        await Assert.That(freshGoal.Step[0].Action.Count).IsEqualTo(1);
        await Assert.That(freshGoal.Step[1].Action.Count).IsEqualTo(0);
    }

    #endregion
}
