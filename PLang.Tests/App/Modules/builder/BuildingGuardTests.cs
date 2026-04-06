using App.Context;
using App.Variables;
using App.Utils;
using App.modules.builder;
using Action = App.Goals.Goal.Steps.Step.Actions.Action.@this;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests the engine.Building.IsEnabled guard — all builder actions should return
/// an error when building is not enabled.
/// </summary>
public class BuildingGuardTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_guard_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
        // Building is NOT enabled — default state
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
    public async Task GetGoals_BuildingDisabled_ReturnsError()
    {
        var action = new goals { Context = _engine.Context, Path = "." };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task GetActions_BuildingDisabled_ReturnsError()
    {
        var action = new GetActions { Context = _engine.Context };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task ValidateActions_BuildingDisabled_ReturnsError()
    {
        var actions = new StepActions { new Action { Module = "file", ActionName = "read" } };
        var action = new validate { Context = _engine.Context, Actions = actions };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task SaveGoals_BuildingDisabled_ReturnsError()
    {
        var goals = new List<Goal> { new Goal { Name = "Test", Path = "/Test.goal" } };
        var action = new goalsSave { Context = _engine.Context, Goals = goals };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task GetApp_BuildingDisabled_ReturnsError()
    {
        var action = new app { Context = _engine.Context, Path = "." };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task SaveApp_BuildingDisabled_ReturnsError()
    {
        var app = new AppData { Id = "x", Version = "0.2" };
        var action = new appSave { Context = _engine.Context, App = app, Path = ".build/app.pr" };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task MergeStep_BuildingDisabled_ReturnsError()
    {
        var step = new Step { Text = "step" };
        var from = new Step { Text = "step" };
        var action = new merge { Context = _engine.Context, Step = step, StepFromLlm = from };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }

    [Test]
    public async Task GetTypeInfo_BuildingDisabled_ReturnsError()
    {
        var action = new types { Context = _engine.Context };
        var result = await _engine.RunAction(action, _engine.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Message).Contains("Building is not enabled");
    }
}
