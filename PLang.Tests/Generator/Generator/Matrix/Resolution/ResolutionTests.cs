using PLang.Tests.App.Fixtures;
using app.module.matrix.resolution;

namespace PLang.Tests.Generator.Matrix.Resolution;

// Matrix entries for the resolution patterns As<T> must support.
// v4 contract: As<T> is the single resolution entry point — fresh walk + substitute + convert per call.
// No caching on Data; backing field on the handler is the only cache, reset per ExecuteAsync.

public class FullVarMatchTests
{
    // Parameter Value "%path%" (full ^%name%$ match) → As<T> calls Variables.Get("path"), returns its Value cast to T.
    [Test]
    public async Task FullVarMatch_StringRef_GetsVariableValue()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<FullVarMatch>(app,
            parameters: new[] { ("path", (object?)"%path%") },
            variables: new Dictionary<string, object?> { ["path"] = "/tmp/x.txt" });

        await result.Data.IsSuccess();
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.ToString()).IsEqualTo("/tmp/x.txt");
    }

    // Variable's Value is itself a Data<T> → As<T> unwraps and returns typed.
    [Test]
    public async Task FullVarMatch_VariableHoldsTypedData_UnwrapsCleanly()
    {
        await using var app = TestApp.Create("/app");
        // Variables.Set wraps the value in Data; the variable's .Value should be unwrapped during As<T>.
        app.User.Context.Variable.Set("count", 42);
        var result = await MatrixRunner.RunAsync<FullVarMatch>(app,
            parameters: new[] { ("path", (object?)"%count%") });

        await result.Data.IsSuccess();
        // FullVarMatch's Path is Data<global::app.type.text.@this>; "42" should be the converted string form.
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.ToString()).IsEqualTo("42");
    }

    // Referenced variable does not exist → As<T> returns Data with null Value (or FromError, per contract).
    [Test]
    [Skip("Resolution-error timing is owned by the eager dispatch-resolve; moves to handler .Value() with the pure-lazy source-gen refactor. See todos 2026-06-15.")]
    public async Task FullVarMatch_MissingVariable_ReturnsErrorOrNotFound()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<FullVarMatch>(app,
            parameters: new[] { ("path", (object?)"%does_not_exist%") });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())).IsNull();
    }
}

public class InterpolationTests
{
    // Parameter Value "Hello %name%" (partial %var%) → As<string> calls Variables.Resolve(str, context), returns interpolated.
    [Test]
    public async Task Interpolation_PartialVar_CallsResolve()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<Interpolation>(app,
            parameters: new[] { ("greeting", (object?)"Hello %name%") },
            variables: new Dictionary<string, object?> { ["name"] = "world" });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.ToString()).IsEqualTo("Hello world");
    }

    // Multiple %var% in one string → all substituted; order preserved.
    [Test]
    public async Task Interpolation_MultipleVars_AllSubstituted()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<Interpolation>(app,
            parameters: new[] { ("greeting", (object?)"%a% then %b% then %a%") },
            variables: new Dictionary<string, object?> { ["a"] = "first", ["b"] = "second" });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.ToString()).IsEqualTo("first then second then first");
    }

    // No %var% in string → returned as-is.
    [Test]
    public async Task Interpolation_NoVars_PassesThrough()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<Interpolation>(app,
            parameters: new[] { ("greeting", (object?)"plain string") });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.ToString()).IsEqualTo("plain string");
    }
}

public class DeepResolutionListTests
{
    // List<object?> { Dict { Content = "%x%" } } → walks list items + dict entries, substitutes %x% in primitives.
    [Test]
    public async Task DeepResolutionList_NestedDict_SubstitutesInside()
    {
        await using var app = TestApp.Create("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%prompt%" }
        };
        var result = await MatrixRunner.RunAsync<DeepResolutionList>(app,
            parameters: new[] { ("messages", (object?)raw) },
            variables: new Dictionary<string, object?> { ["prompt"] = "You are a compiler" });

        var typed = result.Data as global::app.data.@this<global::app.type.list.@this<global::app.module.llm.LlmMessage>>;
        // Read the way a real handler does: enumerate, resolve + convert each row through its door.
        var items = new List<global::app.module.llm.LlmMessage>();
        foreach (var row in (await typed!.Value())!) items.Add((await row.Value()).Clr<global::app.module.llm.LlmMessage>()!);
        await Assert.That(items[0].Content).IsEqualTo("You are a compiler");
    }

    // List items that are themselves nested lists/dicts → recurses correctly to all leaves.
    [Test]
    public async Task DeepResolutionList_NestedListsAndDicts_FullyWalked()
    {
        await using var app = TestApp.Create("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["role"] = "system",
                ["content"] = "%a%"
            },
            new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = "%b%"
            }
        };
        var result = await MatrixRunner.RunAsync<DeepResolutionList>(app,
            parameters: new[] { ("messages", (object?)raw) },
            variables: new Dictionary<string, object?> { ["a"] = "alpha", ["b"] = "beta" });

        var typed = result.Data as global::app.data.@this<global::app.type.list.@this<global::app.module.llm.LlmMessage>>;
        var items = new List<global::app.module.llm.LlmMessage>();
        foreach (var row in (await typed!.Value())!) items.Add((await row.Value()).Clr<global::app.module.llm.LlmMessage>()!);
        await Assert.That(items[0].Content).IsEqualTo("alpha");
        await Assert.That(items[1].Content).IsEqualTo("beta");
    }
}

public class DeepResolutionDictTests
{
    // Dictionary<string, object?> { "Inner" = "%x%" } → walks values, substitutes.
    [Test]
    public async Task DeepResolutionDict_PrimitiveVar_Substituted()
    {
        await using var app = TestApp.Create("/app");
        var raw = new Dictionary<string, object?>
        {
            ["inner"] = "%x%",
            ["other"] = "literal"
        };
        var result = await MatrixRunner.RunAsync<DeepResolutionDict>(app,
            parameters: new[] { ("dict", (object?)raw) },
            variables: new Dictionary<string, object?> { ["x"] = "substituted" });

        var typed = result.Data as global::app.data.@this<global::app.type.dict.@this>;
        // Lazy + stamped (Template="plang" → non-cacheable): resolve the dict through its
        // door, then read each value through ITS door — the real per-item read path.
        var d = (await typed!.Value())!;
        await Assert.That((await d.Get("inner")!.Value()).ToString()).IsEqualTo("substituted");
        await Assert.That((await d.Get("other")!.Value()).ToString()).IsEqualTo("literal");
    }

    // Dictionary value is itself a list → walks both layers.
    [Test]
    public async Task DeepResolutionDict_NestedList_FullyWalked()
    {
        await using var app = TestApp.Create("/app");
        var raw = new Dictionary<string, object?>
        {
            ["items"] = new List<object?> { "%a%", "%b%", "literal" }
        };
        var result = await MatrixRunner.RunAsync<DeepResolutionDict>(app,
            parameters: new[] { ("dict", (object?)raw) },
            variables: new Dictionary<string, object?> { ["a"] = "alpha", ["b"] = "beta" });

        var typed = result.Data as global::app.data.@this<global::app.type.dict.@this>;
        var d = (await typed!.Value())!;
        var inner = new List<string?>();
        foreach (var row in (global::app.type.list.@this)(await d.Get("items")!.Value()))
            inner.Add((await row.Value()).ToString());
        await Assert.That(inner[0]).IsEqualTo("alpha");
        await Assert.That(inner[1]).IsEqualTo("beta");
        await Assert.That(inner[2]).IsEqualTo("literal");
    }
}

public class ReResolveAcrossCallsTests
{
    // Same handler instance, two ExecuteAsync calls with %x% changed between → each picks up the current %x%.
    [Test]
    public async Task ReResolveAcrossCalls_VarChangesBetween_PropertyPicksUpFreshValue()
    {
        await using var app = TestApp.Create("/app");

        app.User.Context.Variable.Set("x", "first");
        var first = await MatrixRunner.RunAsync<ReResolveAcrossCalls>(app,
            parameters: new[] { ("value", (object?)"%x%") });
        var firstTyped = first.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await firstTyped!.Value())?.ToString()).IsEqualTo("first");

        app.User.Context.Variable.Set("x", "second");
        var second = await MatrixRunner.RunAsync<ReResolveAcrossCalls>(app,
            parameters: new[] { ("value", (object?)"%x%") });
        var secondTyped = second.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await secondTyped!.Value())?.ToString()).IsEqualTo("second");
    }

    // Shared Parameter Data — raw .Value never changes across calls.
    [Test]
    public async Task ReResolveAcrossCalls_SharedParameterData_RawValueUnchanged()
    {
        await using var app = TestApp.Create("/app");
        var sharedData = new Data("value", "%x%").Authored();

        app.User.Context.Variable.Set("x", "v1");
        var action1 = new PrAction
        {
            Module = "matrix.resolution",
            ActionName = "reresolveacrosscalls",
            Parameters = new List<Data> { sharedData }
        };
        MatrixRunner.EnsureRegistered<ReResolveAcrossCalls>(app);
        await action1.RunAsync(app.User.Context);

        // The source form is untouched (Peek never renders) — no in-place
        // mutation; Value() on a stamped template renders live by design.
        await Assert.That(sharedData.Peek()?.ToString()).IsEqualTo("%x%");

        app.User.Context.Variable.Set("x", "v2");
        var action2 = new PrAction
        {
            Module = "matrix.resolution",
            ActionName = "reresolveacrosscalls",
            Parameters = new List<Data> { sharedData }
        };
        await action2.RunAsync(app.User.Context);

        await Assert.That(sharedData.Peek()?.ToString()).IsEqualTo("%x%");
    }

    // Loop scenario: same action runs N times with %i% changing each iteration → each read fresh.
    [Test]
    public async Task ReResolveAcrossCalls_LoopIteration_EachReadFresh()
    {
        await using var app = TestApp.Create("/app");
        var seen = new List<string?>();
        for (int i = 0; i < 3; i++)
        {
            app.User.Context.Variable.Set("i", $"value-{i}");
            var r = await MatrixRunner.RunAsync<ReResolveAcrossCalls>(app,
                parameters: new[] { ("value", (object?)"%i%") });
            var typed = r.Data as global::app.data.@this<global::app.type.text.@this>;
            seen.Add((await typed!.Value())?.Clr<string>());
        }
        await Assert.That(seen[0]).IsEqualTo("value-0");
        await Assert.That(seen[1]).IsEqualTo("value-1");
        await Assert.That(seen[2]).IsEqualTo("value-2");
    }
}

public class ConcurrentHandlersTests
{
    // Two handler instances run in parallel → each gets its own backing field; no race.
    [Test]
    public async Task ConcurrentHandlers_ParallelExecuteAsync_NoSharedState()
    {
        await using var app = TestApp.Create("/app");
        app.User.Context.Variable.Set("x", "value");

        // Pre-register; run in parallel.
        MatrixRunner.EnsureRegistered<ConcurrentHandlers>(app);
        var sharedData = new Data("value", "%x%").Authored();

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            var action = new PrAction
            {
                Module = "matrix.resolution",
                ActionName = "concurrenthandlers",
                Parameters = new List<Data> { sharedData }
            };
            var data = await action.RunAsync(app.User.Context);
            return data.Success && (data is global::app.data.@this<global::app.type.text.@this> typed) && (await typed.Value()) == "value";        })).ToArray();

        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(b => b)).IsTrue();
    }

    // 50 parallel typed asks on the same Data resolve consistently — every call
    // yields the value "shared" with no torn state. The ask returns the value
    // instance; an immutable value narrowed by the door may legitimately be the
    // same instance across calls (the cache), so instance-distinctness is not a
    // contract — correctness under concurrency is.
    [Test]
    public async Task ConcurrentHandlers_ParallelAsT_ResolveConsistently()
    {
        await using var app = TestApp.Create("/app");
        app.User.Context.Variable.Set("x", "shared");
        var data = new Data("v", "%x%") { Context = app.User.Context }.Authored();

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            data.Value<global::app.type.text.@this>().AsTask())).ToArray();
        var results = await Task.WhenAll(tasks);

        await Assert.That(results.All(r => r != null)).IsTrue();
        await Assert.That(results.All(r => r!.ToString() == "shared")).IsTrue();
    }
}
