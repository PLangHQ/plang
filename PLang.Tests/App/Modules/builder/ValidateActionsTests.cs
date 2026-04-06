using App.Context;
using App.Variables;
using App.modules.builder;
using Action = App.Goals.Goal.Steps.Step.Actions.Action.@this;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.validateActions — validates LLM-returned actions exist in engine.Modules,
/// resolves GoalCall paths, fills defaults from [Default] attributes.
/// </summary>
public class ValidateActionsTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_validate_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
        _engine.Building.IsEnabled = true;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _engine.DisposeAsync();
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
            new Action { Module = "file", ActionName = "read", Parameters = new List<Data> { new("Path", "test.txt") } }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task ValidateActions_UnknownAction_ReturnsError()
    {
        var actions = new StepActions
        {
            new Action { Module = "nonexistent", ActionName = "fake" }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("nonexistent.fake");
    }

    [Test]
    public async Task ValidateActions_GoalCallPath_Resolved()
    {
        // Create a .pr file that the resolver can find
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        var prJson = System.Text.Json.JsonSerializer.Serialize(new List<Goal>
        {
            new Goal { Name = "DoSomething", Path = "/DoSomething.goal" }
        }, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "dosomething.pr"), prJson);

        var goalCallData = new Data("GoalName", new App.Goals.Goal.GoalCall { Name = "DoSomething" });
        goalCallData.Type = new App.Variables.Type("goal.call");

        var actions = new StepActions
        {
            new Action
            {
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data> { goalCallData }
            }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        // Verify PrPath was actually resolved
        var resolvedCall = actions[0].Parameters[0].Value as App.Goals.Goal.GoalCall;
        await Assert.That(resolvedCall).IsNotNull();
        await Assert.That(resolvedCall!.PrPath).IsEqualTo("/.build/dosomething.pr");
    }

    [Test]
    public async Task ValidateActions_DynamicNames_Skipped()
    {
        var goalCallData = new Data("GoalName", new App.Goals.Goal.GoalCall { Name = "%dynamicGoal%" });
        goalCallData.Type = new App.Variables.Type("goal.call");

        var actions = new StepActions
        {
            new Action
            {
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data> { goalCallData }
            }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task ValidateActions_DefaultsFilled()
    {
        // file.list has [Default("*")] on Pattern and [Default(false)] on Recursive
        var actions = new StepActions
        {
            new Action
            {
                Module = "file",
                ActionName = "list",
                Parameters = new List<Data> { new("Path", "docs/") }
            }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        // Defaults should be filled for missing params
        var fileList = actions[0];
        await Assert.That(fileList.Defaults).IsNotNull();
        await Assert.That(fileList.Defaults!.Count).IsGreaterThan(0);
        var patternDefault = fileList.Defaults.FirstOrDefault(d =>
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
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data>
                {
                    new("Left", "%flag%"),
                    new("Operator", "==") { Type = new App.Variables.Type("string") },
                    new("Right", "false") { Type = new App.Variables.Type("bool") }
                }
            }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var rightParam = actions[0].Parameters.First(p => p.Name == "Right");
        await Assert.That(rightParam.Value).IsEqualTo(false);
        await Assert.That(rightParam.Value is bool).IsTrue();
    }

    [Test]
    public async Task ValidateActions_NormalizesIntStringToInt()
    {
        var actions = new StepActions
        {
            new Action
            {
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data>
                {
                    new("Left", "%count%"),
                    new("Operator", ">") { Type = new App.Variables.Type("string") },
                    new("Right", "5") { Type = new App.Variables.Type("int") }
                }
            }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var rightParam = actions[0].Parameters.First(p => p.Name == "Right");
        await Assert.That(rightParam.Value is int or long).IsTrue();
    }

    [Test]
    public async Task ValidateActions_SkipsVariableReferences()
    {
        var actions = new StepActions
        {
            new Action
            {
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data>
                {
                    new("Left", "%flag%") { Type = new App.Variables.Type("bool") },
                    new("Operator", "=="),
                    new("Right", true) { Type = new App.Variables.Type("bool") }
                }
            }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        // %flag% should NOT be converted — it's a variable reference
        var leftParam = actions[0].Parameters.First(p => p.Name == "Left");
        await Assert.That(leftParam.Value).IsEqualTo("%flag%");
    }

    [Test]
    public async Task ValidateActions_ConfigureDefaults_FromIConfigureT()
    {
        // http.configure implements IConfigure<Config> — defaults come from Config instance, not [Default]
        var actions = new StepActions
        {
            new Action
            {
                Module = "http",
                ActionName = "configure",
                Parameters = new List<Data>()
            }
        };

        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var httpConfigure = actions[0];
        await Assert.That(httpConfigure.Defaults).IsNotNull();
        await Assert.That(httpConfigure.Defaults!.Count).IsGreaterThan(0);
    }
}
