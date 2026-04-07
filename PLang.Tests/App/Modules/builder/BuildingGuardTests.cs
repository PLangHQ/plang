using global::App.Actor.Context;
using global::App.Variables;
using global::App.Utils;
using global::App.modules.builder;
using Action = global::App.Goals.Goal.Steps.Step.Actions.Action.@this;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests the engine.Building.IsEnabled guard — all builder actions should return
/// an error when building is not enabled.
/// </summary>
public class BuildingGuardTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_guard_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
        // Building is NOT enabled — default state
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
    public async Task GetGoals_BuildingDisabled_ReturnsError()
    {
        var action = new goals { Context = _app.Context, Path = "." };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task GetActions_BuildingDisabled_ReturnsError()
    {
        var action = new GetActions { Context = _app.Context };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task ValidateActions_BuildingDisabled_ReturnsError()
    {
        var actions = new StepActions { new Action { Module = "file", ActionName = "read" } };
        var action = new validate { Context = _app.Context, Actions = actions };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task SaveGoals_BuildingDisabled_ReturnsError()
    {
        var goals = new List<Goal> { new Goal { Name = "Test", Path = "/Test.goal" } };
        var action = new goalsSave { Context = _app.Context, Goals = goals };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task GetApp_BuildingDisabled_ReturnsError()
    {
        var action = new app { Context = _app.Context };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task SaveApp_BuildingDisabled_ReturnsError()
    {
        var action = new appSave { Context = _app.Context };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task MergeStep_BuildingDisabled_ReturnsError()
    {
        var step = new Step { Text = "step" };
        var from = new Step { Text = "step" };
        var action = new merge { Context = _app.Context, Step = step, StepFromLlm = from };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task GetTypeInfo_BuildingDisabled_ReturnsError()
    {
        var action = new types { Context = _app.Context };
        var result = await _app.RunAction(action, _app.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }
}
