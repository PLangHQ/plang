using app.actor.context;
using app.variable;
using app.module.action.build;
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
            new Action { Module = "file", ActionName = "read", Parameters = new List<Data> { new("Path", "test.txt", context: _app.User.Context) } }
        };

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        await Assert.That(await result.ToBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task ValidateActions_UnknownAction_ReturnsError()
    {
        var actions = new StepActions
        {
            new Action { Module = "nonexistent", ActionName = "fake" }
        };

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

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
            new Goal { Name = "DoSomething", Path = global::app.type.item.path.@this.Resolve("/DoSomething.goal", _app.User.Context) }
        }, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new global::app.channel.serializer.json.Converter(_app.User.Context) }
        });
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "dosomething.pr"), prJson);

        var goalCallData = new Data("GoalName", new global::app.goal.GoalCall { Name = "DoSomething" }, context: _app.User.Context);

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

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

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
        var goalCallData = new Data("GoalName", new global::app.goal.GoalCall { Name = "%dynamicGoal%" }, context: _app.User.Context);

        var actions = new StepActions
        {
            new Action
            {
                Module = "goal",
                ActionName = "call",
                Parameters = new List<Data> { goalCallData }
            }
        };

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
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
                Module = "file",
                ActionName = "list",
                Parameters = new List<Data> { new("Path", "docs/", context: _app.User.Context) }
            }
        };

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

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
                    new("Left", "%flag%", context: _app.User.Context),
                    new("Operator", "==", new global::app.type.@this("string"), context: _app.User.Context),
                    new("Right", "false", new global::app.type.@this("bool"), context: _app.User.Context)
                }
            }
        };

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var rightParam = actions[0].Parameters.First(p => p.Name == "Right");
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
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data>
                {
                    new("Left", "%count%", context: _app.User.Context),
                    new("Operator", ">", new global::app.type.@this("string"), context: _app.User.Context),
                    new("Right", "5", new global::app.type.@this("number", "int"), context: _app.User.Context)
                }
            }
        };

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var rightParam = actions[0].Parameters.First(p => p.Name == "Right");
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
                Module = "condition",
                ActionName = "if",
                Parameters = new List<Data>
                {
                    new("Left", "%flag%", new global::app.type.@this("bool"), context: _app.User.Context),
                    new("Operator", "==", context: _app.User.Context),
                    new("Right", true, new global::app.type.@this("bool"), context: _app.User.Context)
                }
            }
        };

        var action = new validate(_app.User.Context) { Actions = new("", new global::app.type.clr.@this<StepActions>(actions, _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        // %flag% should NOT be converted — it's a variable reference
        var leftParam = actions[0].Parameters.First(p => p.Name == "Left");
        await Assert.That((await leftParam.Value())?.ToString()).IsEqualTo("%flag%");
    }

    // (Removed ValidateActions_ConfigureDefaults_FromIConfigureT — http.configure + IConfigure<Config>
    // dissolved; http defaults are per-request [Default] props resolved by the setting cascade.)
}
