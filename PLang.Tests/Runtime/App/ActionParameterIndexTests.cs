namespace PLang.Tests.App;

// The action selects the property its step set by name: action[name], null on a miss. What the build
// froze lives in action.Default, where it lives says what it is. A pure lookup: the property is the
// shared program, returned as itself, never resolved.

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
            Property = global::PLang.Tests.Shared.Make.Properties(parameters.Select(p => new Data(p.name, p.value, context: _app.User.Context)).ToList())
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
            Property = global::PLang.Tests.Shared.Make.Properties(parameters.Select(p => new Data(p.name, p.value, context: _app.User.Context)).ToList()),
            Default = global::PLang.Tests.Shared.Make.Properties(defaults.Select(d => new Data(d.name, d.value, context: _app.User.Context)).ToList())
        };
    }

    [Test]
    public async Task Index_FoundInTheStepsProperties_ReturnsThePropertyItself()
    {
        var action = Action(("path", "hello"));

        await Assert.That(ReferenceEquals(action["path"], action.Property[0])).IsTrue();
    }

    [Test]
    public async Task Index_AFrozenDefault_IsNotTheStepsProperty()
    {
        var action = ActionWithDefaults(
            parameters: new[] { ("a", (object?)"x") },
            defaults: new[] { ("b", (object?)"y") });

        await Assert.That(action["b"]).IsNull();
        await Assert.That(ReferenceEquals(action.Default["b"], action.Default[0])).IsTrue();
        await Assert.That(action.Default["b"]!.Value?.ToString()).IsEqualTo("y");
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
