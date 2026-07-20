using app;
using app.variable;
using app.module;

namespace PLang.Tests.App.Core;

public class ActionsTests
{
    [Test]
    public async Task Constructor_Default_CreatesEmptyList()
    {
        var actions = new StepActions();

        await Assert.That(actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithEnumerable_PopulatesList()
    {
        var list = new List<global::app.goal.step.action.@this>
        {
            new() { Module = "variable", ActionName = "set" },
            new() { Module = "file", ActionName = "save" }
        };

        var actions = new StepActions(list);

        await Assert.That(actions.Count).IsEqualTo(2);
    }


    // --- GetActions integration tests (uses real global::app.module.list.@this + assembly discovery) ---

    [Test]
    public async Task GetActions_ReturnsNonEmptyActions()
    {
        var actions = DiscoverActions();

        await Assert.That(actions.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task GetActions_ContainsVariableSet()
    {
        var actions = DiscoverActions();
        var variableSet = actions.FirstOrDefault(a => a.Module == "variable" && a.ActionName == "set");

        await Assert.That(variableSet).IsNotNull();
    }

    [Test]
    public async Task GetActions_ContainsFileSave()
    {
        var actions = DiscoverActions();
        var fileSave = actions.FirstOrDefault(a => a.Module == "file" && a.ActionName == "save");

        await Assert.That(fileSave).IsNotNull();
    }

    [Test]
    public async Task GetActions_ContainsOutputWrite()
    {
        var actions = DiscoverActions();
        var outputWrite = actions.FirstOrDefault(a => a.Module == "output" && a.ActionName == "write");

        await Assert.That(outputWrite).IsNotNull();
    }

    [Test]
    public async Task GetActions_AllActionsHaveClass()
    {
        var actions = DiscoverActions();

        foreach (var action in actions)
        {
            await Assert.That(action.Module).IsNotEqualTo("");
        }
    }

    [Test]
    public async Task GetActions_AllActionsHaveMethod()
    {
        var actions = DiscoverActions();

        foreach (var action in actions)
        {
            await Assert.That(action.ActionName).IsNotEqualTo("");
        }
    }


    // --- ValidateActions tests (uses real global::app.module.list.@this + assembly discovery) ---

    [Test]
    public async Task ValidateActions_NullActions_ReturnsError()
    {
        var (isValid, error) = ValidateActions(null!);

        await Assert.That(isValid).IsEqualTo(false);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("NoActionsProvided");
    }

    [Test]
    public async Task ValidateActions_EmptyActions_ReturnsError()
    {
        var (isValid, error) = ValidateActions(new StepActions());

        await Assert.That(isValid).IsEqualTo(false);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("NoActionsProvided");
    }

    [Test]
    public async Task ValidateActions_ValidActions_ReturnsTrue()
    {
        var actions = new StepActions
        {
            new global::app.goal.step.action.@this { Module = "variable", ActionName = "set" },
            new global::app.goal.step.action.@this { Module = "file", ActionName = "save" }
        };

        var (isValid, error) = ValidateActions(actions);

        await Assert.That(isValid).IsEqualTo(true);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task ValidateActions_OneInvalid_ReturnsActionNotFound()
    {
        var actions = new StepActions
        {
            new global::app.goal.step.action.@this { Module = "bogus", ActionName = "nope" }
        };

        var (isValid, error) = ValidateActions(actions);

        await Assert.That(isValid).IsEqualTo(false);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ActionNotFound");
    }

    [Test]
    public async Task ValidateActions_MixedValidAndInvalid_ReturnsActionNotFound()
    {
        var actions = new StepActions
        {
            new global::app.goal.step.action.@this { Module = "variable", ActionName = "set" },
            new global::app.goal.step.action.@this { Module = "bogus", ActionName = "nope" },
            new global::app.goal.step.action.@this { Module = "fake", ActionName = "missing" }
        };

        var (isValid, error) = ValidateActions(actions);

        await Assert.That(isValid).IsEqualTo(false);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ActionNotFound");
        await Assert.That(error.Message).Contains("bogus.nope");
        await Assert.That(error.Message).Contains("fake.missing");
    }

    // --- MergeStep tests (mirrors PlangModule.MergeStep logic without DI) ---

    [Test]
    public async Task MergeStep_NullStep_ReturnsError()
    {
        var stepFromLlm = new Step { Index = 0, Text = "test" };

        var (result, error) = MergeStep(null!, stepFromLlm);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("MergeError");
    }

    [Test]
    public async Task MergeStep_NullStepFromLlm_ReturnsError()
    {
        var step = new Step { Index = 0, Text = "test" };

        var (result, error) = MergeStep(step, null!);

        await Assert.That(result).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("MergeError");
    }

    [Test]
    public async Task MergeStep_ValidMerge_CopiesActions()
    {
        var step = new Step { Index = 0, Text = "set greeting" };
        var stepFromLlm = new Step
        {
            Actions = new StepActions
            {
                new global::app.goal.step.action.@this { Module = "variable", ActionName = "set" }
            }
        };

        var (result, error) = MergeStep(step, stepFromLlm);

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Actions.Count).IsEqualTo(1);
        await Assert.That(result.Actions[0].Module).IsEqualTo("variable");
        await Assert.That(result.Actions[0].ActionName).IsEqualTo("set");
    }

    [Test]
    public async Task MergeStep_PreservesExistingStepProperties()
    {
        var step = new Step { Index = 3, Text = "original text", LineNumber = 10 };
        var stepFromLlm = new Step
        {
            Actions = new StepActions
            {
                new global::app.goal.step.action.@this { Module = "output", ActionName = "write" }
            }
        };

        var (result, error) = MergeStep(step, stepFromLlm);

        await Assert.That(error).IsNull();
        await Assert.That(result!.Index).IsEqualTo(3);
        await Assert.That(result.Text).IsEqualTo("original text");
        await Assert.That(result.LineNumber).IsEqualTo(10);
    }

    [Test]
    public async Task MergeStep_CopiesErrorsAndWarnings()
    {
        var step = new Step { Index = 0, Text = "test" };
        var stepFromLlm = new Step
        {
            Actions = new StepActions(),
            Errors = new List<Info> { new() { Key = "E1", Message = "Some error" } },
            Warnings = new List<Info> { new() { Key = "W1", Message = "Some warning" } }
        };

        var (result, error) = MergeStep(step, stepFromLlm);

        await Assert.That(error).IsNull();
        await Assert.That(result!.Errors.Count).IsEqualTo(1);
        await Assert.That(result.Errors[0].Key).IsEqualTo("E1");
        await Assert.That(result.Warnings.Count).IsEqualTo(1);
        await Assert.That(result.Warnings[0].Key).IsEqualTo("W1");
    }

    [Test]
    public async Task MergeStep_EmptyActions_ClearsStepActions()
    {
        var step = new Step
        {
            Index = 0,
            Text = "test",
            Actions = new StepActions
            {
                new global::app.goal.step.action.@this { Module = "old", ActionName = "action" }
            }
        };
        var stepFromLlm = new Step { Actions = new StepActions() };

        var (result, error) = MergeStep(step, stepFromLlm);

        await Assert.That(error).IsNull();
        await Assert.That(result!.Actions.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Mirrors PlangModule.MergeStep logic for unit testing without DI.
    /// </summary>
    private static (Step? step, global::app.error.IError? error) MergeStep(Step step, Step stepFromLlm)
    {
        if (step == null)
            return (null, new global::app.error.ProgramError("Step cannot be null", key: "MergeError"));
        if (stepFromLlm == null)
            return (null, new global::app.error.ProgramError("Step result from LLM cannot be null", key: "MergeError"));

        step.Actions.Clear();
        step.Actions.AddRange(stepFromLlm.Actions);

        if (stepFromLlm.Errors.Count > 0)
        {
            step.Errors.Clear();
            step.Errors.AddRange(stepFromLlm.Errors);
        }
        if (stepFromLlm.Warnings.Count > 0)
        {
            step.Warnings.Clear();
            step.Warnings.AddRange(stepFromLlm.Warnings);
        }

        return (step, null);
    }

    /// <summary>
    /// Mirrors PlangModule.ValidateActions logic for unit testing without DI.
    /// </summary>
    private static (bool isValid, global::app.error.IError? error) ValidateActions(StepActions actions)
    {
        if (actions == null || actions.Count == 0)
            return (false, new global::app.error.ProgramError("No actions provided", key: "NoActionsProvided"));

        var modules = new global::app.module.list.@this();

        var notFound = new List<string>();
        foreach (var action in actions)
        {
            if (!modules.Contains(action.Module, action.ActionName))
                notFound.Add($"{action.Module}.{action.ActionName}");
        }

        if (notFound.Count > 0)
            return (false, new global::app.error.ProgramError(
                $"Actions not found: {string.Join(", ", notFound)}", key: "ActionNotFound"));

        return (true, null);
    }

    /// <summary>
    /// Mimics what GetActions() in PlangModule does — uses global::app.module.list.@this to discover handlers.
    /// </summary>
    private static StepActions DiscoverActions()
    {
        var modules = new global::app.module.list.@this();

        var actions = new StepActions();

        foreach (var ns in modules.Names)
        {
            foreach (var actionName in modules.GetActions(ns))
            {
                actions.Add(new global::app.goal.step.action.@this
                {
                    Module = ns,
                    ActionName = actionName,
                });
            }
        }

        return actions;
    }
}
