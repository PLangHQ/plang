using System.Text.Json;
using App.Engine.Context;
using App.Engine.Variables;
using App.modules.builder;
using PLangEngine = App.Engine.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.saveGoals — serializes goals to a v0.2 .pr file.
/// One .goal file → one .pr file containing List&lt;Goal&gt;.
/// </summary>
public class SaveGoalsTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_savegoals_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task SaveGoals_SerializesToPrPath()
    {
        var goals = new List<Goal>
        {
            new Goal
            {
                Name = "Start",
                Path = "/Start.goal",
                Steps = new GoalSteps
                {
                    new Step { Text = "write hello", Index = 0 }
                }
            }
        };

        var action = new goalsSave { Context = _engine.Context, Goals = goals };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();

        // Verify file content
        var prPath = System.IO.Path.Combine(_tempDir, ".build", "start.pr");
        var json = System.IO.File.ReadAllText(prPath);
        var saved = JsonSerializer.Deserialize<List<Goal>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.Count).IsEqualTo(1);
        await Assert.That(saved[0].Name).IsEqualTo("Start");
        await Assert.That(saved[0].Steps.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SaveGoals_CamelCase_StoreOnly()
    {
        var goals = new List<Goal>
        {
            new Goal
            {
                Name = "Test",
                Path = "/Test.goal",
                Description = null
            }
        };

        var action = new goalsSave { Context = _engine.Context, Goals = goals };
        await _engine.RunAction(action, _engine.Context);

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
    public async Task SaveGoals_MultipleGoals_SingleFile()
    {
        var goals = new List<Goal>
        {
            new Goal { Name = "Public", Path = "/Multi.goal" },
            new Goal { Name = "Private", Path = "/Multi.goal" }
        };

        var action = new goalsSave { Context = _engine.Context, Goals = goals };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();

        var prPath = System.IO.Path.Combine(_tempDir, ".build", "multi.pr");
        var json = System.IO.File.ReadAllText(prPath);
        var deserialized = JsonSerializer.Deserialize<List<Goal>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SaveGoals_EmptyGoalsList_ReturnsError()
    {
        var action = new goalsSave { Context = _engine.Context, Goals = new List<Goal>() };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NoGoals");
    }

    [Test]
    public async Task SaveGoals_NoPrPath_ReturnsError()
    {
        var goals = new List<Goal> { new Goal { Name = "Test" } }; // No Path → no PrPath
        var action = new goalsSave { Context = _engine.Context, Goals = goals };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NoPrPath");
    }
}
