namespace PLang.Tests.App.DataTests;

// Integration-level resolution tests for v4.
// Earlier this file asserted snapshot-once .Value semantics (the OPPOSITE contract) — replaced.
// The replacements assert that:
//   1. Resolution is fresh per call (As<T> walk; no cache on Data).
//   2. Sharing a Data instance across actions is safe (Data is stateless w.r.t. resolution).
//   3. Loop iteration / sub-goal calls each see their own resolved view from a single raw Data.

public class DataResolutionTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Same Parameter Data, two As<string> calls with different Variables.Get("x") between → two different results.
    [Test]
    public async Task SharedParameterData_AsTBetweenChanges_YieldsTwoResults()
    {
        var data = new Data("v", "%x%") { Context = _app.User.Context };

        _app.User.Context.Variables.Set("x", "first");
        var first = data.As<string>(_app.User.Context);
        _app.User.Context.Variables.Set("x", "second");
        var second = data.As<string>(_app.User.Context);

        await Assert.That(first.Value).IsEqualTo("first");
        await Assert.That(second.Value).IsEqualTo("second");
    }

    // After As<T>, original Data._value is byte-for-byte the same as before — no in-place mutation.
    [Test]
    public async Task AsT_DoesNotMutateOriginalRaw()
    {
        _app.User.Context.Variables.Set("name", "world");
        var raw = new List<object?> { "%name%", "literal" };
        var data = new Data("list", raw) { Context = _app.User.Context };

        var resolved = data.As<List<string>>(_app.User.Context);
        await Assert.That(resolved.Value![0]).IsEqualTo("world");

        // Original raw is unchanged.
        await Assert.That(((List<object?>)data.Value!)[0]).IsEqualTo("%name%");
        await Assert.That(ReferenceEquals(data.Value, raw)).IsTrue();
    }

    // Loop iteration scenario: action runs N times, %i% changes each iteration → property reads N distinct values.
    [Test]
    public async Task LoopIteration_PropertyResolvesPerCall()
    {
        var data = new Data("v", "%i%") { Context = _app.User.Context };

        var seen = new List<string?>();
        for (int i = 0; i < 3; i++)
        {
            _app.User.Context.Variables.Set("i", $"value-{i}");
            seen.Add(data.As<string>(_app.User.Context).Value);
        }

        await Assert.That(seen[0]).IsEqualTo("value-0");
        await Assert.That(seen[1]).IsEqualTo("value-1");
        await Assert.That(seen[2]).IsEqualTo("value-2");
    }

    // Sub-goal scenario: parent's parameter Data is read by parent, then by sub-goal with different vars →
    //   each goal sees its own resolved view; raw Data is unchanged.
    [Test]
    public async Task SubGoalCall_EachGoalSeesOwnResolvedView()
    {
        var data = new Data("v", "%scope%") { Context = _app.User.Context };
        _app.User.Context.Variables.Set("scope", "parent");
        var parentView = data.As<string>(_app.User.Context);

        await using var subApp = new global::app.@this("/sub");
        subApp.User.Context.Variables.Set("scope", "sub");
        var subView = data.As<string>(subApp.User.Context);

        await Assert.That(parentView.Value).IsEqualTo("parent");
        await Assert.That(subView.Value).IsEqualTo("sub");
        // Raw is untouched.
        await Assert.That(data.Value).IsEqualTo("%scope%");
    }

    // Variables.Get returns existing Data → As<T> on a parameter referencing that variable returns its Value cleanly.
    [Test]
    public async Task FullVarMatch_VariableHoldsData_UnwrappedCleanly()
    {
        _app.User.Context.Variables.Set("count", 42);
        var data = new Data("c", "%count%") { Context = _app.User.Context };

        var result = data.As<int>(_app.User.Context);
        await Assert.That(result.Value).IsEqualTo(42);
    }

    // List<LlmMessage> with nested %comment% → first call resolves to "value1", set %comment%="value2", second call resolves to "value2".
    [Test]
    public async Task DeepResolution_ListWithVar_RefreshesPerCall()
    {
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%comment%" }
        };
        var data = new Data("messages", raw) { Context = _app.User.Context };

        _app.User.Context.Variables.Set("comment", "value1");
        var first = data.As<List<global::app.module.llm.LlmMessage>>(_app.User.Context);
        await Assert.That(first.Value![0].Content).IsEqualTo("value1");

        _app.User.Context.Variables.Set("comment", "value2");
        var second = data.As<List<global::app.module.llm.LlmMessage>>(_app.User.Context);
        await Assert.That(second.Value![0].Content).IsEqualTo("value2");
    }

    // Concurrent As<T> calls on the same parameter Data → no race, two valid (independent) results.
    [Test]
    public async Task ConcurrentAsT_OnSharedParameterData_NoRace()
    {
        _app.User.Context.Variables.Set("x", "value");
        var data = new Data("v", "%x%") { Context = _app.User.Context };

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var r = data.As<string>(_app.User.Context);
                if (r.Value != "value") return false;
            }
            return true;
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(b => b)).IsTrue();
    }
}
