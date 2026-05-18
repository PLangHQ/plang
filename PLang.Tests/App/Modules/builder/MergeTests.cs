using app;
using global::app.goals.goal;
using global::app.goals.goal.steps.step;
using global::app.variables;
using Action = global::app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for Step.Merge() and Goal.MergeFrom() — OBP methods that own
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
            Actions = new StepActions(new[]
            {
                new Action { Module = "output", ActionName = "write", Parameters = new List<Data> { new("Message", "hello") } }
            })
        };

        target.Merge(source);

        await Assert.That(target.Actions.Count).IsEqualTo(1);
        await Assert.That(target.Actions[0].Module).IsEqualTo("output");
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
            Actions = new StepActions(new[]
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
        await Assert.That(target.Actions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task StepMerge_EmptySource_LeavesTargetUnchanged()
    {
        var originalAction = new Action { Module = "output", ActionName = "write" };
        var target = new Step
        {
            Text = "step",
            Actions = new StepActions(new[] { originalAction })
        };
        var source = new Step { Text = "step" }; // Empty LLM fields

        target.Merge(source);

        // Actions not cleared because source has 0 actions
        await Assert.That(target.Actions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task StepMerge_ErrorsReplacedOnlyWhenSourceHasEntries()
    {
        var target = new Step
        {
            Text = "step",
            Errors = { new Info { Key = "E1", Message = "original error" } }
        };

        // Source with no errors — target keeps its errors
        var emptySource = new Step { Text = "step" };
        target.Merge(emptySource);
        await Assert.That(target.Errors.Count).IsEqualTo(1);
        await Assert.That(target.Errors[0].Key).IsEqualTo("E1");

        // Source with errors — target errors replaced
        var sourceWithErrors = new Step
        {
            Text = "step",
            Errors = { new Info { Key = "E2", Message = "new error" } }
        };
        target.Merge(sourceWithErrors);
        await Assert.That(target.Errors.Count).IsEqualTo(1);
        await Assert.That(target.Errors[0].Key).IsEqualTo("E2");
    }

    #endregion

    #region Goal.MergeFrom

    [Test]
    public async Task GoalMergeFrom_MatchesByText_MergesLlmFields()
    {
        var freshGoal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step { Text = "do something", Index = 0 },
                new Step { Text = "do another thing", Index = 1 }
            }
        };

        var existingGoal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step
                {
                    Text = "do something",
                    Actions = new StepActions(new[]
                    {
                        new Action { Module = "output", ActionName = "write" }
                    })
                }
            }
        };

        freshGoal.MergeFrom(existingGoal);

        // Matched step gets actions
        await Assert.That(freshGoal.Steps[0].Actions.Count).IsEqualTo(1);
        // Unmatched step keeps empty
        await Assert.That(freshGoal.Steps[1].Actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalMergeFrom_UnmatchedSteps_KeepEmptyActions()
    {
        var freshGoal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step { Text = "new step that didn't exist before", Index = 0 }
            }
        };

        var existingGoal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step
                {
                    Text = "old step text",
                    Actions = new StepActions(new[]
                    {
                        new Action { Module = "file", ActionName = "read" }
                    })
                }
            }
        };

        freshGoal.MergeFrom(existingGoal);

        await Assert.That(freshGoal.Steps[0].Actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalMergeFrom_NullExisting_NoOp()
    {
        var freshGoal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps { new Step { Text = "step", Index = 0 } }
        };

        // Should not throw
        freshGoal.MergeFrom(null);

        await Assert.That(freshGoal.Steps.Count).IsEqualTo(1);
        await Assert.That(freshGoal.Steps[0].Actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GoalMergeFrom_DuplicateStepText_FirstMatchOnly()
    {
        // Two steps with identical text in fresh goal, one match in existing
        var freshGoal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step { Text = "do something", Index = 0 },
                new Step { Text = "do something", Index = 1 }
            }
        };

        var existingGoal = new Goal
        {
            Name = "Test",
            Steps = new GoalSteps
            {
                new Step
                {
                    Text = "do something",
                    Actions = new StepActions(new[]
                    {
                        new Action { Module = "output", ActionName = "write" }
                    })
                }
            }
        };

        freshGoal.MergeFrom(existingGoal);

        // First match gets the actions, second stays empty
        await Assert.That(freshGoal.Steps[0].Actions.Count).IsEqualTo(1);
        await Assert.That(freshGoal.Steps[1].Actions.Count).IsEqualTo(0);
    }

    #endregion
}
