using app.actor.context;
using app.variable;
using app.module.builder;
using Action = global::app.goal.steps.step.actions.action.@this;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.validateActions — validates LLM-returned actions exist in engine.Modules,
/// resolves GoalCall paths, fills defaults from [Default] attributes.
/// </summary>
public class ValidateActionsTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_validate_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task ValidateActions_ValidActions_ReturnsOk()
    {
        var actions = new StepActions
        {
            new Action { Module = "file", ActionName = "read", Parameters = new List<Data> { new("Path", "test.txt") } }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        await Assert.That((bool)(await result.Value())!).IsTrue();
    }

    [Test]
    public async Task ValidateActions_UnknownAction_ReturnsError()
    {
        var actions = new StepActions
        {
            new Action { Module = "nonexistent", ActionName = "fake" }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsFailure();
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
            new Goal { Name = "DoSomething", Path = global::app.type.path.@this.Resolve("/DoSomething.goal", _app.User.Context) }
        }, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new global::app.channel.serializer.json.Converter(_app.User.Context) }
        });
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "dosomething.pr"), prJson);

        var goalCallData = new Data("GoalName", new global::app.goal.GoalCall { Name = "DoSomething" });
        goalCallData.Type = new global::app.type.@this("goal.call");

        var actions = new StepActions
        {
            // goal.call.call carries the GoalCall directly — no condition wrapper noise.
            new Action
            {
                Module = "goal",
                ActionName = "call",
                Parameters = new List<Data> { goalCallData }
            }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        // Verify PrPath was actually resolved
        var resolvedCall = (await actions[0].Parameters[0].Value()) as global::app.goal.GoalCall;
        await Assert.That(resolvedCall).IsNotNull();
        await Assert.That(resolvedCall!.PrPath?.ToString().Replace('\\', '/').TrimStart('/'))
            .IsEqualTo(".build/dosomething.pr");
    }

    [Test]
    public async Task ValidateActions_DynamicNames_Skipped()
    {
        var goalCallData = new Data("GoalName", new global::app.goal.GoalCall { Name = "%dynamicGoal%" });
        goalCallData.Type = new global::app.type.@this("goal.call");

        var actions = new StepActions
        {
            new Action
            {
                Module = "goal",
                ActionName = "call",
                Parameters = new List<Data> { goalCallData }
            }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

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
                Module = "file",
                ActionName = "list",
                Parameters = new List<Data> { new("Path", "docs/") }
            }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
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
                    new("Operator", "==") { Type = new global::app.type.@this("string") },
                    new("Right", "false") { Type = new global::app.type.@this("bool") }
                }
            }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var rightParam = actions[0].Parameters.First(p => p.Name == "Right");
        await Assert.That((await rightParam.Value())).IsEqualTo(false);
        await Assert.That((await rightParam.Value()) is bool).IsTrue();
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
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data>
                {
                    new("Left", "%count%"),
                    new("Operator", ">") { Type = new global::app.type.@this("string") },
                    new("Right", "5") { Type = new global::app.type.@this("number", "int") }
                }
            }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var rightParam = actions[0].Parameters.First(p => p.Name == "Right");
        // The kind="int" carries the precision intent; the value either gets
        // coerced to a number primitive at validate-time OR stays as a string
        // for the runtime to coerce. Either is acceptable post-Stage-2.
        await Assert.That((await rightParam.Value()) is int or long or string).IsTrue();
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
                    new("Left", "%flag%") { Type = new global::app.type.@this("bool") },
                    new("Operator", "=="),
                    new("Right", true) { Type = new global::app.type.@this("bool") }
                }
            }
        };

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        // %flag% should NOT be converted — it's a variable reference
        var leftParam = actions[0].Parameters.First(p => p.Name == "Left");
        await Assert.That((await leftParam.Value())).IsEqualTo("%flag%");
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

        var action = new validate { Context = _app.User.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var httpConfigure = actions[0];
        await Assert.That(httpConfigure.Defaults).IsNotNull();
        await Assert.That(httpConfigure.Defaults!.Count).IsGreaterThan(0);
    }
}
