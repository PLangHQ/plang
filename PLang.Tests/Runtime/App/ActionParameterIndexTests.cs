namespace PLang.Tests.App;

// The action selects its parameter row by name: action[name] — Parameter first, then Default, null
// on a miss. A pure lookup: the row is the shared program, returned as itself, never resolved.

public class ActionParameterIndexTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Plain("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private PrAction Action(params (string name, object? value)[] parameters)
    {
        return new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["test"],
            Name = "fixture",
            Parameter = parameters.Select(p => new Data(p.name, p.value, context: _app.User.Context)).ToList()
        };
    }

    private PrAction ActionWithDefaults(
        IEnumerable<(string name, object? value)> parameters,
        IEnumerable<(string name, object? value)> defaults)
    {
        return new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["test"],
            Name = "fixture",
            Parameter = parameters.Select(p => new Data(p.name, p.value, context: _app.User.Context)).ToList(),
            Default = defaults.Select(d => new Data(d.name, d.value, context: _app.User.Context)).ToList()
        };
    }

    [Test]
    public async Task Index_FoundInParameters_ReturnsTheRowItself()
    {
        var action = Action(("path", "hello"));

        await Assert.That(ReferenceEquals(action["path"], action.Parameter[0])).IsTrue();
    }

    [Test]
    public async Task Index_FallsBackToDefaults_WhenNotInParameters()
    {
        var action = ActionWithDefaults(
            parameters: new[] { ("a", (object?)"x") },
            defaults: new[] { ("b", (object?)"y") });

        var found = action["b"];

        await Assert.That(ReferenceEquals(found, action.Default![0])).IsTrue();
        await Assert.That((await found!.Value())?.ToString()).IsEqualTo("y");
    }

    [Test]
    public async Task Index_Missing_IsNull()
    {
        await Assert.That(Action(("path", "hello"))["missing"]).IsNull();
    }

    [Test]
    public async Task Index_CaseInsensitive_MatchesAcrossCasings()
    {
        var action = Action(("Path", "hello"));

        await Assert.That(ReferenceEquals(action["path"], action["PATH"])).IsTrue();
        await Assert.That(ReferenceEquals(action["path"], action["PaTh"])).IsTrue();
        await Assert.That(action["path"]).IsNotNull();
    }

    [Test]
    public async Task Index_EmptyLists_IsNull()
    {
        var action = new PrAction { Module = global::PLang.Tests.TestApp.SharedContext.App.Module["test"], Name = "fixture" };

        await Assert.That(action["anything"]).IsNull();
    }
}
