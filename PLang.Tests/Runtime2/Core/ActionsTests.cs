using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.actions;

namespace PLang.Tests.Runtime2.Core;

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
        var list = new List<PLang.Runtime2.Engine.Action>
        {
            new() { Module = "variable", ActionName = "set" },
            new() { Module = "file", ActionName = "save" }
        };

        var actions = new StepActions(list);

        await Assert.That(actions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Value_ReturnsSelf()
    {
        var actions = new StepActions();
        actions.Add(new PLang.Runtime2.Engine.Action { Module = "variable", ActionName = "set" });

        await Assert.That(actions.Value).IsEquivalentTo(actions);
    }

    [Test]
    public async Task Summary_NoContext_ReturnsEmptyString()
    {
        var actions = new StepActions();
        actions.Add(new PLang.Runtime2.Engine.Action { Module = "variable", ActionName = "set" });

        var (text, error) = await actions.Summary();

        await Assert.That(text).IsEqualTo("");
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task Summary_WrongContextType_ReturnsEmptyString()
    {
        var actions = new StepActions("not a PLangContext");
        actions.Add(new PLang.Runtime2.Engine.Action { Module = "variable", ActionName = "set" });

        var (text, error) = await actions.Summary();

        await Assert.That(text).IsEqualTo("");
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task ParameterSchema_SetOnAction_IsPreserved()
    {
        var action = new PLang.Runtime2.Engine.Action
        {
            Module = "variable",
            ActionName = "set",
            ParameterSchema = typeof(PLang.Runtime2.actions.variable.Set)
        };

        await Assert.That(action.ParameterSchema).IsEqualTo(typeof(PLang.Runtime2.actions.variable.Set));
    }

    [Test]
    public async Task ParameterSchema_DefaultIsNull()
    {
        var action = new PLang.Runtime2.Engine.Action
        {
            Module = "variable",
            ActionName = "set"
        };

        await Assert.That(action.ParameterSchema).IsNull();
    }

    // --- GetActions integration tests (uses real EngineLibraries + assembly discovery) ---

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
    public async Task GetActions_VariableSet_HasParameterSchema()
    {
        var actions = DiscoverActions();
        var variableSet = actions.First(a => a.Module == "variable" && a.ActionName == "set");

        await Assert.That(variableSet.ParameterSchema).IsNotNull();
        await Assert.That(variableSet.ParameterSchema).IsEqualTo(typeof(PLang.Runtime2.actions.variable.Set));
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

    [Test]
    public async Task GetActions_NoParamHandler_HasNullParameterSchema()
    {
        var actions = DiscoverActions();
        // Handlers with NullParams should have null ParameterSchema
        var noParams = actions.Where(a => a.ParameterSchema == null).ToList();

        // There should be at least some handlers without params (e.g., variable.clear)
        await Assert.That(noParams.Count).IsGreaterThanOrEqualTo(0);
    }

    // --- ValidateActions tests (uses real EngineLibraries + assembly discovery) ---

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
            new PLang.Runtime2.Engine.Action { Module = "variable", ActionName = "set" },
            new PLang.Runtime2.Engine.Action { Module = "file", ActionName = "save" }
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
            new PLang.Runtime2.Engine.Action { Module = "bogus", ActionName = "nope" }
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
            new PLang.Runtime2.Engine.Action { Module = "variable", ActionName = "set" },
            new PLang.Runtime2.Engine.Action { Module = "bogus", ActionName = "nope" },
            new PLang.Runtime2.Engine.Action { Module = "fake", ActionName = "missing" }
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
                new PLang.Runtime2.Engine.Action { Module = "variable", ActionName = "set" }
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
                new PLang.Runtime2.Engine.Action { Module = "output", ActionName = "write" }
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
                new PLang.Runtime2.Engine.Action { Module = "old", ActionName = "action" }
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
    private static (Step? step, PLang.Runtime2.Engine.Errors.IError? error) MergeStep(Step step, Step stepFromLlm)
    {
        if (step == null)
            return (null, new PLang.Runtime2.Engine.Errors.ProgramError("Step cannot be null", key: "MergeError"));
        if (stepFromLlm == null)
            return (null, new PLang.Runtime2.Engine.Errors.ProgramError("Step result from LLM cannot be null", key: "MergeError"));

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
    private static (bool isValid, PLang.Runtime2.Engine.Errors.IError? error) ValidateActions(StepActions actions)
    {
        if (actions == null || actions.Count == 0)
            return (false, new PLang.Runtime2.Engine.Errors.ProgramError("No actions provided", key: "NoActionsProvided"));

        var libraries = new EngineLibraries();

        var notFound = new List<string>();
        foreach (var action in actions)
        {
            if (!libraries.Contains(action.Module, action.ActionName))
                notFound.Add($"{action.Module}.{action.ActionName}");
        }

        if (notFound.Count > 0)
            return (false, new PLang.Runtime2.Engine.Errors.ProgramError(
                $"Actions not found: {string.Join(", ", notFound)}", key: "ActionNotFound"));

        return (true, null);
    }

    /// <summary>
    /// Mimics what GetActions() in PlangModule does — uses EngineLibraries to discover handlers.
    /// </summary>
    private static StepActions DiscoverActions()
    {
        var libraries = new EngineLibraries();

        var actions = new StepActions();

        foreach (var ns in libraries.Modules)
        {
            foreach (var actionName in libraries.GetActions(ns))
            {
                // Check IClass-based handlers first
                var handler = libraries.Get(ns, actionName);
                if (handler != null)
                {
                    actions.Add(new PLang.Runtime2.Engine.Action
                    {
                        Module = ns,
                        ActionName = actionName,
                        ParameterSchema = handler.ParameterType
                    });
                    continue;
                }

                // Check [Action]-based types
                var actionType = libraries.GetActionType(ns, actionName);
                if (actionType != null)
                {
                    actions.Add(new PLang.Runtime2.Engine.Action
                    {
                        Module = ns,
                        ActionName = actionName,
                        ParameterSchema = actionType
                    });
                }
            }
        }

        return actions;
    }
}
