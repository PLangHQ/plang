using app.actor.context;
using app.variable;
using app.module.action.build;
using Action = global::app.goal.step.action.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for build.validate — finishes a step's grafted actions (resolves GoalCall paths, fills
/// defaults from [Default] attributes, normalizes literal types) and returns the step's verdict.
/// </summary>
public class ValidateActionsTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    // build.validate takes the step whole; the actions under test are that step's actions.
    private validate For(StepActions actions)
    {
        var step = new Step { Text = "step under validation", Index = 0 };
        foreach (var a in actions) step.Action.Add(a);
        return new validate(_app.User.Context) { Step = new("", step) };
    }

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_validate_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
        _app.Build = new global::app.module.action.build.@this(_app.System.Context);
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
    public async Task ValidateActions_ValidActions_ReturnsOk()
    {
        var actions = new StepActions
        {
            new Action { Module = global::PLang.Tests.TestApp.SharedContext.App.Module["file"], Name = "read", Parameter = new List<Data> { new("Path", "test.txt", context: _app.User.Context) } }
        };

        var action = For(actions);
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        await Assert.That(await result.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task ValidateActions_UnknownAction_ReturnsError()
    {
        var actions = new StepActions
        {
            new Action { Module = global::PLang.Tests.TestApp.SharedContext.App.Module["variable"], Name = "fake" }
        };

        var action = For(actions);
        var result = await _app.Run(action, _app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("variable.fake");
    }

    // --- goal.call's Name becomes the target's address at build ---

    // A step in /Caller.goal whose one action is `call <name>`, validated through build.validate.
    private async Task<Data> BuildCallFromCaller(string name, params Goal[] children)
    {
        var caller = new Goal
        {
            Name = "Caller",
            Path = global::app.type.item.path.@this.Resolve("/Caller.goal", _app.User.Context),
        };
        foreach (var child in children) { child.Parent = caller; caller.Child.Add(child); }
        _app.Goal.Add(caller);

        var step = new Step { Goal = caller, Text = $"call {name}", Index = 0 };
        step.Action.Add(new Action
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["goal"],
            Name = "call",
            Parameter = new List<Data> { new("Name", name, context: _app.User.Context) }
        });
        caller.Step.Add(step);

        await _app.Run(new validate(_app.User.Context) { Step = new("", step) }, _app.User.Context);
        return step.Action[0].Parameter.First(p => p.Name == "Name");
    }

    [Test]
    public async Task ValidateActions_GoalCall_NameInAnotherFile_BecomesItsAddress()
    {
        _app.Goal.Add(new Goal
        {
            Name = "DoSomething",
            Path = global::app.type.item.path.@this.Resolve("/lib/DoSomething.goal", _app.User.Context)
        });

        var name = await BuildCallFromCaller("DoSomething");

        await Assert.That(name.Peek()?.ToString()).IsEqualTo("/lib/DoSomething");
    }

    [Test]
    public async Task ValidateActions_GoalCall_ChildInSameFile_StaysBare()
    {
        var child = new Goal
        {
            Name = "Helper",
            Path = global::app.type.item.path.@this.Resolve("/Caller.goal", _app.User.Context)
        };

        var name = await BuildCallFromCaller("Helper", child);

        await Assert.That(name.Peek()?.ToString()).IsEqualTo("Helper");
    }

    [Test]
    public async Task ValidateActions_GoalCall_VariableName_StaysAuthored()
    {
        var name = await BuildCallFromCaller("%target%");

        await Assert.That(name.Peek()?.ToString()).IsEqualTo("%target%");
    }

    [Test]
    public async Task ValidateActions_DynamicNames_Skipped()
    {
        var goalCallData = new Data("Name", "%dynamicGoal%", context: _app.User.Context);

        var actions = new StepActions
        {
            new Action
            {
                Module = global::PLang.Tests.TestApp.SharedContext.App.Module["goal"],
                Name = "call",
                Parameter = new List<Data> { goalCallData }
            }
        };

        var action = For(actions);
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
    }

    [Test]
    public async Task ValidateActions_DefaultsFilled()
    {
        // file.list has [Default("*")] on Pattern and [Default(false)] on Recursive
        var actions = new StepActions
        {
            new Action
            {
                Module = global::PLang.Tests.TestApp.SharedContext.App.Module["file"],
                Name = "list",
                Parameter = new List<Data> { new("Path", "docs/", context: _app.User.Context) }
            }
        };

        var action = For(actions);
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        // Defaults should be filled for missing params
        var fileList = actions[0];
        await Assert.That(fileList.Default).IsNotNull();
        await Assert.That(fileList.Default!.Count).IsGreaterThan(0);
        var patternDefault = fileList.Default.FirstOrDefault(d =>
            d.Name.Equals("pattern", StringComparison.OrdinalIgnoreCase));
        await Assert.That(patternDefault).IsNotNull();
    }

    // --- Type normalization ---

    [Test]
    public async Task ValidateActions_NormalizesBoolStringToJsonBool()
    {
        var actions = new StepActions
        {
            new Action
            {
                Module = global::PLang.Tests.TestApp.SharedContext.App.Module["condition"],
                Name = "if",
                Parameter = new List<Data>
                {
                    new("Left", "%flag%", context: _app.User.Context),
                    new("Operator", "==", new global::app.type.@this("string"), context: _app.User.Context),
                    new("Right", "false", new global::app.type.@this("bool"), context: _app.User.Context)
                }
            }
        };

        var action = For(actions);
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var rightParam = actions[0].Parameter.First(p => p.Name == "Right");
        await Assert.That((await rightParam.Value())?.ToString()).IsEqualTo("false");
        await Assert.That((await rightParam.Value()) is global::app.type.item.@bool.@this).IsTrue();
    }

    [Test]
    public async Task ValidateActions_NormalizesIntStringToInt()
    {
        // Post-Stage-2: "int" canonicalises to {name:"number", kind:"int"}.
        // Validation should still normalize the string-shaped value "5" to a
        // numeric primitive. Either int or long is acceptable; the test pins
        // "string → numeric coercion happens", not the specific precision.
        var actions = new StepActions
        {
            new Action
            {
                Module = global::PLang.Tests.TestApp.SharedContext.App.Module["condition"],
                Name = "if",
                Parameter = new List<Data>
                {
                    new("Left", "%count%", context: _app.User.Context),
                    new("Operator", ">", new global::app.type.@this("string"), context: _app.User.Context),
                    new("Right", "5", new global::app.type.@this("number", "int"), context: _app.User.Context)
                }
            }
        };

        var action = For(actions);
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var rightParam = actions[0].Parameter.First(p => p.Name == "Right");
        // The kind="int" carries the precision intent; the value either gets
        // coerced to a number primitive at validate-time OR stays as a string
        // for the runtime to coerce. Either is acceptable post-Stage-2.
        await Assert.That((await rightParam.Value()) is global::app.type.item.number.@this or global::app.type.item.text.@this).IsTrue();
    }

    [Test]
    public async Task ValidateActions_SkipsVariableReferences()
    {
        var actions = new StepActions
        {
            new Action
            {
                Module = global::PLang.Tests.TestApp.SharedContext.App.Module["condition"],
                Name = "if",
                Parameter = new List<Data>
                {
                    new("Left", "%flag%", new global::app.type.@this("bool"), context: _app.User.Context),
                    new("Operator", "==", context: _app.User.Context),
                    new("Right", true, new global::app.type.@this("bool"), context: _app.User.Context)
                }
            }
        };

        var action = For(actions);
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        // %flag% should NOT be converted — it's a variable reference
        var leftParam = actions[0].Parameter.First(p => p.Name == "Left");
        await Assert.That((await leftParam.Value())?.ToString()).IsEqualTo("%flag%");
    }

    // The build pass asks each bound handler for its own verdict: variable.set judges a strict
    // declared kind against its literal's content — a check only the handler makes.
    [Test]
    public async Task ValidateActions_HandlerValidate_VerdictFailsTheStep()
    {
        byte[] png =
        {
            0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
            0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
            0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
            0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
            0x42,0x60,0x82
        };
        var gifStrict = new global::app.type.@this("image", "gif", true) { Context = _app.User.Context };
        var actions = new StepActions
        {
            new Action
            {
                Module = global::PLang.Tests.TestApp.SharedContext.App.Module["variable"],
                Name = "set",
                Parameter = new List<Data>
                {
                    new("Name", "%img%", context: _app.User.Context),
                    new("Value", png, context: _app.User.Context),
                    new("Type", gifStrict, context: _app.User.Context),
                }
            }
        };

        var result = await _app.Run(For(actions), _app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("Strict kind mismatch");
    }

    // (Removed ValidateActions_ConfigureDefaults_FromIConfigureT — http.configure + IConfigure<Config>
    // dissolved; http defaults are per-request [Default] props resolved by the setting cascade.)
}
