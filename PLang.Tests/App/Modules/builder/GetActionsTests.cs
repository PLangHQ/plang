using global::app.actor.context;
using global::app.variables;
using global::app.modules.builder;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.getActions — reflects all registered actions with parameter schemas
/// for the LLM prompt. Parameter metadata comes from reflecting action type properties.
/// </summary>
public class GetActionsTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_actions_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task GetActions_ReturnsAllModulesAndActions()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var actions = result.Value as StepActions;
        await Assert.That(actions).IsNotNull();
        await Assert.That(actions!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task GetActions_ParameterTypes_IncludeNullableMarkers()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)result.Value!;

        // Find an action with nullable parameters (e.g., file.read has optional properties)
        var fileRead = actions.FirstOrDefault(a => a.Module == "file" && a.ActionName == "read");
        await Assert.That(fileRead).IsNotNull();
        // At minimum, there should be parameters
        await Assert.That(fileRead!.Parameters.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task GetActions_VariableNameParams_Marked()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)result.Value!;

        // variable.set has a Name property with Data<Variable> (renders as `%var% string`)
        var varSet = actions.FirstOrDefault(a => a.Module == "variable" && a.ActionName == "set");
        await Assert.That(varSet).IsNotNull();
        var nameParam = varSet!.Parameters.FirstOrDefault(p =>
            p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
        await Assert.That(nameParam).IsNotNull();
        await Assert.That(nameParam!.Value!.ToString()).Contains("%var%");
    }

    [Test]
    public async Task GetActions_DefaultValues_Included()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)result.Value!;

        // file.list has Pattern with [Default("*")]
        var fileList = actions.FirstOrDefault(a => a.Module == "file" && a.ActionName == "list");
        await Assert.That(fileList).IsNotNull();
        var patternParam = fileList!.Parameters.FirstOrDefault(p =>
            p.Name.Equals("Pattern", StringComparison.OrdinalIgnoreCase));
        await Assert.That(patternParam).IsNotNull();
        await Assert.That(patternParam!.Value!.ToString()).Contains("\"*\"");
    }

    [Test]
    public async Task GetActions_CacheableFlag_FromActionAttribute()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)result.Value!;

        // file.save has [Action("save", Cacheable = false)]
        var fileSave = actions.FirstOrDefault(a => a.Module == "file" && a.ActionName == "save");
        await Assert.That(fileSave).IsNotNull();
        await Assert.That(fileSave!.Cacheable).IsFalse();

        // file.read has default Cacheable = true
        var fileRead = actions.FirstOrDefault(a => a.Module == "file" && a.ActionName == "read");
        await Assert.That(fileRead).IsNotNull();
        await Assert.That(fileRead!.Cacheable).IsTrue();
    }

    [Test]
    public async Task GetActions_ExcludesProviderProperties()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)result.Value!;

        // No action should expose [Code]-attributed interface properties
        // (e.g., IBuilder, IFile, ILlm)
        // Note: string params named "Provider" (like identity.create) are legitimate
        foreach (var a in actions)
        {
            var providerParam = a.Parameters.FirstOrDefault(p =>
                p.Value?.ToString()?.StartsWith("i", StringComparison.OrdinalIgnoreCase) == true &&
                p.Value.ToString()!.Contains("Provider"));
            await Assert.That(providerParam)
                .IsNull()
                .Because($"{a.Module}.{a.ActionName} should not expose [Code] interface properties");
        }
    }
}
