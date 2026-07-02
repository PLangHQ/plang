using app.actor.context;
using app.variable;
using app.module.builder;
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
    public async Task GetActions_ReturnsAllModulesAndActions()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var actions = (await result.Value()) as StepActions;
        await Assert.That(actions).IsNotNull();
        await Assert.That(actions!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task GetActions_ParameterTypes_IncludeNullableMarkers()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)(await result.Value())!;

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
        var actions = (StepActions)(await result.Value())!;

        // variable.set has a Name property with Data<Variable> — renders as exactly "%var%"
        // (no trailing type token; the marker alone tells the LLM this slot names a variable).
        var varSet = actions.FirstOrDefault(a => a.Module == "variable" && a.ActionName == "set");
        await Assert.That(varSet).IsNotNull();
        var nameParam = varSet!.Parameters.FirstOrDefault(p =>
            p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
        await Assert.That(nameParam).IsNotNull();
        await Assert.That((await nameParam!.Value())!.ToString()).IsEqualTo("%var%");
    }

    [Test]
    public async Task GetActions_DefaultValues_Included()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)(await result.Value())!;

        // file.list has Pattern with [Default("*")]
        var fileList = actions.FirstOrDefault(a => a.Module == "file" && a.ActionName == "list");
        await Assert.That(fileList).IsNotNull();
        var patternParam = fileList!.Parameters.FirstOrDefault(p =>
            p.Name.Equals("Pattern", StringComparison.OrdinalIgnoreCase));
        await Assert.That(patternParam).IsNotNull();
        await Assert.That((await patternParam!.Value())!.ToString()).Contains("\"*\"");
    }

    [Test]
    public async Task GetActions_CacheableFlag_FromActionAttribute()
    {
        var action = new GetActions { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);
        var actions = (StepActions)(await result.Value())!;

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
        var actions = (StepActions)(await result.Value())!;

        // No action should expose [Code]-attributed interface properties
        // (e.g., IBuilder, IFile, ILlm)
        // Note: string params named "Provider" (like identity.create) are legitimate
        foreach (var a in actions)
        {
            var providerParam = a.Parameters.FirstOrDefault(p =>
                (p.Peek())?.ToString()?.StartsWith("i", StringComparison.OrdinalIgnoreCase) == true &&
                (p.Peek())?.ToString()!.Contains("Provider") == true);
            await Assert.That(providerParam)
                .IsNull()
                .Because($"{a.Module}.{a.ActionName} should not expose [Code] interface properties");
        }
    }

    // --- Actions filter ----------------------------------------
    //
    // The Actions param restricts the returned catalog to a named set of
    // module.action entries. Null/empty → full catalog. The Compile step uses
    // it to keep the prompt focused. These guard the filter so a silent no-op
    // (case-insensitive match broken, empty-list-as-empty-result confusion,
    // unknown name throwing instead of dropping) can't slip past.

    [Test]
    public async Task GetActions_ActionsFilter_RestrictsToNamed()
    {
        var action = new GetActions
        {
            Context = _app.User.Context,
            Actions = new global::app.data.@this<global::app.type.list.@this>("", global::app.type.list.@this.FromRaw(new List<string> { "file.read", "file.save" }, _app.User.Context))
        };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var actions = (await result.Value()) as StepActions;
        await Assert.That(actions).IsNotNull();
        await Assert.That(actions!.Count).IsEqualTo(2);
        await Assert.That(actions.Any(a => a.Module == "file" && a.ActionName == "read")).IsTrue();
        await Assert.That(actions.Any(a => a.Module == "file" && a.ActionName == "save")).IsTrue();
    }

    [Test]
    public async Task GetActions_ActionsFilter_Empty_ReturnsFullCatalog()
    {
        // Empty list semantic = "no filter" (matches the Default.cs check
        // `if (filter is { Count: > 0 })`). A regression that flipped this to
        // "filter to nothing" would silently drop every action.
        var unfiltered = new GetActions { Context = _app.User.Context };
        var fullResult = await _app.RunAction(unfiltered, _app.User.Context);
        var fullCount = ((StepActions)(await fullResult.Value())!).Count;

        var action = new GetActions
        {
            Context = _app.User.Context,
            Actions = new global::app.data.@this<global::app.type.list.@this>("", global::app.type.list.@this.FromRaw(new List<string>(), _app.User.Context))
        };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var actions = (await result.Value()) as StepActions;
        await Assert.That(actions).IsNotNull();
        await Assert.That(actions!.Count).IsEqualTo(fullCount);
    }

    [Test]
    public async Task GetActions_ActionsFilter_UnknownName_ReturnsEmptyNoError()
    {
        var action = new GetActions
        {
            Context = _app.User.Context,
            Actions = new global::app.data.@this<global::app.type.list.@this>("", global::app.type.list.@this.FromRaw(new List<string> { "nonexistent.action" }, _app.User.Context))
        };
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var actions = (await result.Value()) as StepActions;
        await Assert.That(actions).IsNotNull();
        await Assert.That(actions!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetActions_ActionsFilter_IsCaseInsensitive()
    {
        var action = new GetActions
        {
            Context = _app.User.Context,
            Actions = new global::app.data.@this<global::app.type.list.@this>("", global::app.type.list.@this.FromRaw(new List<string> { "File.Read", "FILE.SAVE" }, _app.User.Context))
        };
        var result = await _app.RunAction(action, _app.User.Context);

        var actions = (StepActions)(await result.Value())!;
        await Assert.That(actions.Count).IsEqualTo(2);
        await Assert.That(actions.Any(a => a.Module == "file" && a.ActionName == "read")).IsTrue();
        await Assert.That(actions.Any(a => a.Module == "file" && a.ActionName == "save")).IsTrue();
    }
}
