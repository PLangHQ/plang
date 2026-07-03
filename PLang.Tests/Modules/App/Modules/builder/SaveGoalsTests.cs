using System.Text.Json;
using app.actor.context;
using app.variable;
using app.module.builder;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.saveGoals — serializes a goal to a v0.2 .pr file.
/// One .goal file → one .pr file containing a single Goal (with optional sub-goals in .Goals).
/// </summary>
public class SaveGoalsTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_savegoals_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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
    public async Task SaveGoal_SerializesToPrPath()
    {
        var step = new Step { Text = "write hello", Index = 0 };
        step.Actions.Add(new PrAction { Module = "output", ActionName = "write" });
        var goal = new Goal
        {
            Name = "Start",
            Path = global::app.type.path.@this.Resolve("/Start.goal", _app.User.Context),
            Steps = new GoalSteps { step }
        };

        var action = new goalsSave(_app.User.Context) { Goal = goal };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();

        // Verify file content
        var prPath = System.IO.Path.Combine(_tempDir, ".build", "start.pr");
        var json = System.IO.File.ReadAllText(prPath);
        var saved = JsonSerializer.Deserialize<Goal>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new global::app.channel.serializer.json.Converter(_app.User.Context) } });
        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.Name).IsEqualTo("Start");
        await Assert.That(saved.Steps.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SaveGoal_CamelCase_StoreOnly()
    {
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.path.@this.Resolve("/Test.goal", _app.User.Context),
            Description = null
        };

        var action = new goalsSave(_app.User.Context) { Goal = goal };
        await _app.RunAction(action, _app.User.Context);

        var prPath = System.IO.Path.Combine(_tempDir, ".build", "test.pr");
        var json = System.IO.File.ReadAllText(prPath);

        // Should use camelCase
        await Assert.That(json).Contains("\"name\"");
        // Null [Store] properties included for determinism
        await Assert.That(json).Contains("\"description\"");
        // Non-[Store] properties should not appear
        await Assert.That(json).DoesNotContain("\"errors\"");
        await Assert.That(json).DoesNotContain("\"warnings\"");
    }

    [Test]
    public async Task SaveGoal_WithSubGoals_SingleFile()
    {
        var goal = new Goal
        {
            Name = "Public",
            Path = global::app.type.path.@this.Resolve("/Multi.goal", _app.User.Context),
            Goals = new List<Goal>
            {
                new Goal { Name = "Private" }
            }
        };

        var action = new goalsSave(_app.User.Context) { Goal = goal };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();

        var prPath = System.IO.Path.Combine(_tempDir, ".build", "multi.pr");
        var json = System.IO.File.ReadAllText(prPath);
        var saved = JsonSerializer.Deserialize<Goal>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new global::app.channel.serializer.json.Converter(_app.User.Context) } });

        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.Name).IsEqualTo("Public");
        await Assert.That(saved.Goals.Count).IsEqualTo(1);
        await Assert.That(saved.Goals[0].Name).IsEqualTo("Private");
    }

    [Test]
    public async Task SaveGoal_NoPrPath_ReturnsError()
    {
        var goal = new Goal { Name = "Test" }; // No Path → no PrPath
        var action = new goalsSave(_app.User.Context) { Goal = goal };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoPrPath");
    }
}
