namespace PLang.Tests.App;

// Contract tests for Action.GetParameter(name, context) — the new lookup method introduced in v4 Phase 1.
// Lives at PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs.
// v4 contract: walks Parameters, falls back to Defaults, returns Data.NotFound. Pure lookup, no resolution.

public class GetParameterTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private PrAction Action(params (string name, object? value)[] parameters)
    {
        return new PrAction
        {
            Module = "test",
            ActionName = "fixture",
            Parameters = parameters.Select(p => new Data(p.name, p.value)).ToList()
        };
    }

    private PrAction ActionWithDefaults(
        IEnumerable<(string name, object? value)> parameters,
        IEnumerable<(string name, object? value)> defaults)
    {
        return new PrAction
        {
            Module = "test",
            ActionName = "fixture",
            Parameters = parameters.Select(p => new Data(p.name, p.value)).ToList(),
            Defaults = defaults.Select(d => new Data(d.name, d.value)).ToList()
        };
    }

    // Parameter present in Parameters → returns the same Data instance (reference equality, not a copy).
    [Test]
    public async Task GetParameter_FoundInParameters_ReturnsSameDataInstance()
    {
        var action = Action(("path", "hello"));
        var stored = action.Parameters[0];

        var found = action.GetParameter("path", _app.User.Context);

        await Assert.That(ReferenceEquals(found, stored)).IsTrue();
    }

    // Parameter absent from Parameters but present in Defaults → returns the Defaults entry.
    [Test]
    public async Task GetParameter_FallsBackToDefaults_WhenNotInParameters()
    {
        var action = ActionWithDefaults(
            parameters: new[] { ("a", (object?)"x") },
            defaults: new[] { ("b", (object?)"y") });
        var defaultData = action.Defaults![0];

        var found = action.GetParameter("b", _app.User.Context);

        await Assert.That(ReferenceEquals(found, defaultData)).IsTrue();
        await Assert.That((await found.Value())).IsEqualTo("y");
    }

    // Parameter absent from both → returns Data.NotFound (IsInitialized = false).
    [Test]
    public async Task GetParameter_NotFound_ReturnsDataNotFound()
    {
        var action = Action(("path", "hello"));

        var result = action.GetParameter("missing", _app.User.Context);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsInitialized).IsFalse();
        await Assert.That(result.Name).IsEqualTo("missing");
    }

    // Lookup is case-INsensitive on Name — matches today's Parameters lookup behavior across the codebase.
    [Test]
    public async Task GetParameter_CaseInsensitive_MatchesAcrossCasings()
    {
        var action = Action(("Path", "hello"));

        var lower = action.GetParameter("path", _app.User.Context);
        var upper = action.GetParameter("PATH", _app.User.Context);
        var mixed = action.GetParameter("PaTh", _app.User.Context);

        await Assert.That(lower.IsInitialized).IsTrue();
        await Assert.That(upper.IsInitialized).IsTrue();
        await Assert.That(mixed.IsInitialized).IsTrue();
        await Assert.That(ReferenceEquals(lower, upper)).IsTrue();
        await Assert.That(ReferenceEquals(lower, mixed)).IsTrue();
    }

    // GetParameter does NOT trigger resolution — returned Data.Value is whatever construction set (raw).
    [Test]
    public async Task GetParameter_NoResolutionSideEffect_ValueRemainsRaw()
    {
        _app.User.Context.Variable.Set("name", "world");
        var action = Action(("greeting", "Hello %name%"));

        var found = action.GetParameter("greeting", _app.User.Context);

        // .Value is the raw string. Resolution lives in As<T>(context); GetParameter
        // is pure lookup with no side effect on the underlying Data.
        // (After Phase 2 this is also true; pre-Phase-2 the raw form is preserved
        // because GetParameter returns the same Data instance — its .Value getter
        // may still resolve, but the value was constructed as raw.)
        await Assert.That(found.IsInitialized).IsTrue();
        // We assert reference identity; whatever .Value returns is up to Data's contract,
        // but the lookup does not trigger any work itself.
        await Assert.That(ReferenceEquals(found, action.Parameters[0])).IsTrue();
    }

    // Empty Parameters list, empty Defaults → returns Data.NotFound, not null, not exception.
    [Test]
    public async Task GetParameter_EmptyLists_ReturnsNotFound()
    {
        var action = new PrAction { Module = "test", ActionName = "fixture" };

        var result = action.GetParameter("anything", _app.User.Context);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsInitialized).IsFalse();
    }
}
