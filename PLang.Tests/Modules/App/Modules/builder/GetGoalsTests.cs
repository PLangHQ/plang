using System.Text.Json;
using app.actor.context;
using app.variable;
using app.module.action.build;
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
    public async Task GetGoals_UnreadableSources_FailTheBuildOnce_NamingEveryFile_EachErrorWhole()
    {
        // Two .goal files outside the app root. The actor holds an EXACT read grant on their folder, so
        // the listing passes; each file read is ungranted and asks — a closed input answers "no" — so
        // both reads are denied. The build reads every file, then fails once naming each.
        var outside = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_unreadable_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(outside);
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "A.goal"), "A\n- a step");
            System.IO.File.WriteAllText(System.IO.Path.Combine(outside, "B.goal"), "B\n- b step");

            var ctx = _app.User.Context;
            var folder = global::app.type.item.path.@this.Resolve("/" + outside, ctx);   // OS-absolute: out of root
            var grant = global::app.type.item.permission.@this.Request(ctx.Actor!.Name, folder.Absolute,
                global::app.type.item.permission.Verb.Read, global::app.type.item.permission.Match.Exact);
            await ctx.Actor.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: ctx), false);

            var action = new goals(ctx) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(folder) };
            var result = await _app.Run(action, ctx);

            await result.IsFailure();
            await Assert.That(result.Error!.Key).IsEqualTo("FileReadError");
            await Assert.That(result.Error.Message).Contains("A.goal");
            await Assert.That(result.Error.Message).Contains("B.goal");
            await Assert.That(result.Error.list.Count).IsEqualTo(2);
            // Each cause is the read's own error, whole: the gate denied the read (no consent could be had).
            await Assert.That(result.Error.list.All(e => e.Key == "PermissionDenied")).IsTrue()
                .Because(string.Join(" || ", result.Error.list.Select(e => $"{e.Key}: {e.Message}")));
        }
        finally { System.IO.Directory.Delete(outside, true); }
    }

    [Test]
    public async Task GetGoals_ParsesGoalFilesFromFolder()
    {
        // Write a .goal file
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Start.goal"),
            "Start\n- write out 'hello'\n- set %x% = 1");

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var goals = result.GetValue<List<Goal>>();
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(goals[0].Name).IsEqualTo("Start");
        await Assert.That(goals[0].Step.Count).IsEqualTo(2);
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

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);
        var goals = result.GetValue<List<Goal>>();

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
            Path = global::app.type.item.path.@this.Resolve("/Start.goal", global::PLang.Tests.TestApp.SharedContext),
            Step = new GoalSteps
            {
                new Step
                {
                    Text = "write out 'hello'",
                    Code = new StepActions(new[]
                    {
                        new global::app.goal.step.action.@this
                        {
                            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["output"],
                            Name = "write",
                            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data> { new("Data", "hello", context: _app.User.Context) })
                        }
                    })
                }
            }
        };
        // Write the .pr the way the builder now does — the goal writes itself via Output (Store),
        // through the channel serializer (not the deleted STJ PrWrite options).
        var prSerializer = (global::app.channel.serializer.plang.@this)
            _app.User.Channel.Serializers.GetByMimeType("application/plang");
        using var prMs = new System.IO.MemoryStream();
        await prSerializer.SerializeItemAsync(prMs, prGoal, global::app.View.Store);
        var prJson = System.Text.Encoding.UTF8.GetString(prMs.ToArray());
        System.IO.File.WriteAllText(System.IO.Path.Combine(buildDir, "start.pr"), prJson);

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);
        var goals = result.GetValue<List<Goal>>();

        await Assert.That(goals).IsNotNull();
        var startGoal = goals!.FirstOrDefault(g => g.Name == "Start");
        await Assert.That(startGoal).IsNotNull();
        // Merged actions from .pr data
        await Assert.That(startGoal!.Step[0].Code.Count).IsEqualTo(1);
        await Assert.That(startGoal.Step[0].Code[0].Module.Name).IsEqualTo("output");
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
        _app.Build.Files.Add(new global::app.type.item.path.file.@this("Start.goal"));

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var goals = result.GetValue<List<Goal>>();
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
        _app.Build.Files.Add(new global::app.type.item.path.file.@this("mygoal.goal"));

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var goals = result.GetValue<List<Goal>>();
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsEqualTo(1);
        await Assert.That(goals[0].Name).IsEqualTo("MyGoal");
    }

    [Test]
    public async Task GetGoals_FilesFilter_NoMatch_FailsNamingTheFilter()
    {
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_tempDir, "Start.goal"),
            "Start\n- write out 'hello'");

        // Filter for a file that doesn't exist
        _app.Build.Files.Add(new global::app.type.item.path.file.@this("NonExistent.goal"));

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoGoalMatched");
        await Assert.That(result.Error.Message).Contains("NonExistent.goal");
    }

    [Test]
    public async Task GetGoals_FilesFilter_RelativeEntry_IsFromTheAppRoot()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_tempDir, "Sanity"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(_tempDir, "Sanity", "AddItem.goal"), "AddItem\n- write out 'x'");
        System.IO.File.WriteAllText(System.IO.Path.Combine(_tempDir, "Other.goal"), "Other\n- write out 'other'");

        // as typed at the app's root: no leading slash
        _app.Build.Files.Add(new global::app.type.item.text.@this("Sanity/AddItem.goal"));

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var goals = result.GetValue<List<Goal>>();
        await Assert.That(goals!.Select(g => g.Name)).IsEquivalentTo(new[] { "AddItem" });
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

        _app.Build.Files.Add(new global::app.type.item.path.file.@this("First.goal"));
        _app.Build.Files.Add(new global::app.type.item.path.file.@this("Third.goal"));

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var goals = result.GetValue<List<Goal>>();
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsEqualTo(2);
        await Assert.That(goals.Any(g => g.Name == "First")).IsTrue();
        await Assert.That(goals.Any(g => g.Name == "Third")).IsTrue();
    }

    [Test]
    public async Task GetGoals_EmptyFolder_ReturnsEmptyList()
    {
        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var goals = result.GetValue<List<Goal>>();
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

        var action = new goals(_app.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(".", _app.User.Context)) };
        var result = await _app.Run(action, _app.User.Context);

        await result.IsSuccess();
        var goals = result.GetValue<List<Goal>>();
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Count).IsGreaterThanOrEqualTo(1);
        // Steps should have empty actions (no merge happened)
        await Assert.That(goals[0].Step[0].Code.Count).IsEqualTo(0);
        // The corrupt .pr is a build diagnostic about the goal that rebuilt — it hangs on that goal
        await Assert.That(goals[0].Warning.Any(w => w.Key == "CorruptPrFile")).IsTrue();
    }
}
