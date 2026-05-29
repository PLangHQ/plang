using System.Text.Json;
using app.actor.context;
using app.variable;
using app.modules.builder;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.getGoals — finds .goal files under a path, parses via GoalFile,
/// filters out system goals, and merges existing .pr data by matching goal names.
/// </summary>
public class GetGoalsTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_getgoals_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task GetGoals_ParsesGoalFilesFromFolder()
    {
        // Write a .goal file
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Start.goal"),
            "Start\n- write out 'hello'\n- set %x% = 1");

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var goals = result.Value as List<Goal>;
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(goals[0].Name).IsEqualTo("Start");
        await Assert.That(goals[0].Steps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetGoals_MarksSystemGoals()
    {
        // Write a regular .goal file
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "MyGoal.goal"),
            "MyGoal\n- step one");

        // Write a system .goal file
        var systemDir = System.IO.Path.Combine(_tempDir, "system");
        System.IO.Directory.CreateDirectory(systemDir);
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(systemDir, "Build.goal"),
            "Build\n- build step");

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);
        var goals = result.Value as List<Goal>;

        await Assert.That(goals).IsNotNull();
        // System goals should be present but marked
        var systemGoal = goals!.FirstOrDefault(g => g.Name == "Build");
        await Assert.That(systemGoal).IsNotNull();
        await Assert.That(systemGoal!.IsSystem).IsTrue();
        var userGoal = goals.FirstOrDefault(g => g.Name == "MyGoal");
        await Assert.That(userGoal).IsNotNull();
        await Assert.That(userGoal!.IsSystem).IsFalse();
    }

    [Test]
    public async Task GetGoals_MergesExistingPrData()
    {
        // Write a .goal file
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Start.goal"),
            "Start\n- write out 'hello'");

        // Write existing .pr data with actions for that step
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);

        var prGoal = new Goal
        {
            Name = "Start",
            Path = "/Start.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Text = "write out 'hello'",
                    Actions = new StepActions(new[]
                    {
                        new global::app.goal.steps.step.actions.action.@this
                        {
                            Module = "output",
                            ActionName = "write",
                            Parameters = new List<Data> { new("Message", "hello") }
                        }
                    })
                }
            }
        };
        var prJson = JsonSerializer.Serialize(new List<Goal> { prGoal }, global::app.Utils.Json.PrWrite);
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "start.pr"), prJson);

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);
        var goals = result.Value as List<Goal>;

        await Assert.That(goals).IsNotNull();
        var startGoal = goals!.FirstOrDefault(g => g.Name == "Start");
        await Assert.That(startGoal).IsNotNull();
        // Merged actions from .pr data
        await Assert.That(startGoal!.Steps[0].Actions.Count).IsEqualTo(1);
        await Assert.That(startGoal.Steps[0].Actions[0].Module).IsEqualTo("output");
    }

    [Test]
    public async Task GetGoals_FilesFilter_ReturnsOnlyMatchingGoals()
    {
        // Write two .goal files
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Start.goal"),
            "Start\n- write out 'hello'");
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Other.goal"),
            "Other\n- write out 'other'");

        // Set files filter to only build Start.goal
        _app.Builder.Files.Add(new global::app.type.path.file.@this("Start.goal"));

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var goals = result.Value as List<Goal>;
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsEqualTo(1);
        await Assert.That(goals[0].Name).IsEqualTo("Start");
    }

    [Test]
    public async Task GetGoals_FilesFilter_CaseInsensitive()
    {
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "MyGoal.goal"),
            "MyGoal\n- step one");

        // Filter with different casing
        _app.Builder.Files.Add(new global::app.type.path.file.@this("mygoal.goal"));

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var goals = result.Value as List<Goal>;
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsEqualTo(1);
        await Assert.That(goals[0].Name).IsEqualTo("MyGoal");
    }

    [Test]
    public async Task GetGoals_FilesFilter_NoMatch_ReturnsEmptyList()
    {
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Start.goal"),
            "Start\n- write out 'hello'");

        // Filter for a file that doesn't exist
        _app.Builder.Files.Add(new global::app.type.path.file.@this("NonExistent.goal"));

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var goals = result.Value as List<Goal>;
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetGoals_FilesFilter_MultipleFiles()
    {
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "First.goal"),
            "First\n- step one");
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Second.goal"),
            "Second\n- step two");
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Third.goal"),
            "Third\n- step three");

        _app.Builder.Files.Add(new global::app.type.path.file.@this("First.goal"));
        _app.Builder.Files.Add(new global::app.type.path.file.@this("Third.goal"));

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var goals = result.Value as List<Goal>;
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsEqualTo(2);
        await Assert.That(goals.Any(g => g.Name == "First")).IsTrue();
        await Assert.That(goals.Any(g => g.Name == "Third")).IsTrue();
    }

    [Test]
    public async Task GetGoals_EmptyFolder_ReturnsEmptyList()
    {
        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var goals = result.Value as List<Goal>;
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetGoals_CorruptPrFile_IgnoresAndReparses()
    {
        // Write a .goal file
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Start.goal"),
            "Start\n- some step");

        // Write corrupt .pr file
        var buildDir = System.IO.Path.Combine(_tempDir, ".build");
        System.IO.Directory.CreateDirectory(buildDir);
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(buildDir, "start.pr"),
            "{ invalid json {{{}}}");

        var action = new goals { Context = _app.User.Context, Path = global::app.data.@this<global::app.type.path.@this>.Ok(global::app.type.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var goals = result.Value as List<Goal>;
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsGreaterThanOrEqualTo(1);
        // Steps should have empty actions (no merge happened)
        await Assert.That(goals[0].Steps[0].Actions.Count).IsEqualTo(0);
        // Warnings should contain the corrupt file error
        await Assert.That(result.Warnings).IsNotNull();
        await Assert.That(result.Warnings!.Any(w => w.Key == "CorruptPrFile")).IsTrue();
    }
}
