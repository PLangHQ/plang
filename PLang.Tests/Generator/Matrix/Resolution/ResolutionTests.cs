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
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<FullVarMatch>(app,
            parameters: new[] { ("path", (object?)"%path%") },
            variables: new Dictionary<string, object?> { ["path"] = "/tmp/x.txt" });

        await result.Data.IsSuccess();
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(typed!.Value).IsEqualTo("/tmp/x.txt");
    }

    // Variable's Value is itself a Data<T> → As<T> unwraps and returns typed.
    [Test]
    public async Task FullVarMatch_VariableHoldsTypedData_UnwrapsCleanly()
    {
        await using var app = new global::app.@this("/app");
        // Variables.Set wraps the value in Data; the variable's .Value should be unwrapped during As<T>.
        app.User.Context.Variable.Set("count", 42);
        var result = await MatrixRunner.RunAsync<FullVarMatch>(app,
            parameters: new[] { ("path", (object?)"%count%") });

        await result.Data.IsSuccess();
        // FullVarMatch's Path is Data<global::app.type.text.@this>; "42" should be the converted string form.
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(typed!.Value).IsEqualTo("42");
    }

    // Referenced variable does not exist → As<T> returns Data with null Value (or FromError, per contract).
    [Test]
    public async Task FullVarMatch_MissingVariable_ReturnsErrorOrNotFound()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<FullVarMatch>(app,
            parameters: new[] { ("path", (object?)"%does_not_exist%") });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(typed!.Value).IsNull();
    }
}

public class InterpolationTests
{
    // Parameter Value "Hello %name%" (partial %var%) → As<string> calls Variables.Resolve(str, context), returns interpolated.
    [Test]
    public async Task Interpolation_PartialVar_CallsResolve()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<Interpolation>(app,
            parameters: new[] { ("greeting", (object?)"Hello %name%") },
            variables: new Dictionary<string, object?> { ["name"] = "world" });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(typed!.Value).IsEqualTo("Hello world");
    }

    // Multiple %var% in one string → all substituted; order preserved.
    [Test]
    public async Task Interpolation_MultipleVars_AllSubstituted()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<Interpolation>(app,
            parameters: new[] { ("greeting", (object?)"%a% then %b% then %a%") },
            variables: new Dictionary<string, object?> { ["a"] = "first", ["b"] = "second" });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(typed!.Value).IsEqualTo("first then second then first");
    }

    // No %var% in string → returned as-is.
    [Test]
    public async Task Interpolation_NoVars_PassesThrough()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<Interpolation>(app,
            parameters: new[] { ("greeting", (object?)"plain string") });

        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(typed!.Value).IsEqualTo("plain string");
    }
}

public class DeepResolutionListTests
{
    // List<object?> { Dict { Content = "%x%" } } → walks list items + dict entries, substitutes %x% in primitives.
    [Test]
    public async Task DeepResolutionList_NestedDict_SubstitutesInside()
    {
        await using var app = new global::app.@this("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%prompt%" }
        };
        var result = await MatrixRunner.RunAsync<DeepResolutionList>(app,
            parameters: new[] { ("messages", (object?)raw) },
            variables: new Dictionary<string, object?> { ["prompt"] = "You are a compiler" });

        var typed = result.Data as global::app.data.@this<global::app.type.list.@this<global::app.module.llm.LlmMessage>>;
        await Assert.That(typed!.Value).IsNotNull();
        await Assert.That(typed.Value![0].Content).IsEqualTo("You are a compiler");
    }

    // List items that are themselves nested lists/dicts → recurses correctly to all leaves.
    [Test]
    public async Task DeepResolutionList_NestedListsAndDicts_FullyWalked()
    {
        await using var app = new global::app.@this("/app");
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
        await Assert.That(typed!.Value![0].Content).IsEqualTo("alpha");
        await Assert.That(typed.Value![1].Content).IsEqualTo("beta");
    }
}

public class DeepResolutionDictTests
{
    // Dictionary<string, object?> { "Inner" = "%x%" } → walks values, substitutes.
    [Test]
    public async Task DeepResolutionDict_PrimitiveVar_Substituted()
    {
        await using var app = new global::app.@this("/app");
        var raw = new Dictionary<string, object?>
        {
            ["inner"] = "%x%",
            ["other"] = "literal"
        };
        var result = await MatrixRunner.RunAsync<DeepResolutionDict>(app,
            parameters: new[] { ("dict", (object?)raw) },
            variables: new Dictionary<string, object?> { ["x"] = "substituted" });

        var typed = result.Data as global::app.data.@this<global::app.type.dict.@this>;
        await Assert.That(typed!.Value!["inner"]).IsEqualTo("substituted");
        await Assert.That(typed.Value["other"]).IsEqualTo("literal");
    }

    // Dictionary value is itself a list → walks both layers.
    [Test]
    public async Task DeepResolutionDict_NestedList_FullyWalked()
    {
        await using var app = new global::app.@this("/app");
        var raw = new Dictionary<string, object?>
        {
            ["items"] = new List<object?> { "%a%", "%b%", "literal" }
        };
        var result = await MatrixRunner.RunAsync<DeepResolutionDict>(app,
            parameters: new[] { ("dict", (object?)raw) },
            variables: new Dictionary<string, object?> { ["a"] = "alpha", ["b"] = "beta" });

        var typed = result.Data as global::app.data.@this<global::app.type.dict.@this>;
        var inner = typed!.Value!["items"] as List<object?>;
        await Assert.That(inner![0]).IsEqualTo("alpha");
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
        await using var app = new global::app.@this("/app");

        app.User.Context.Variable.Set("x", "first");
        var first = await MatrixRunner.RunAsync<ReResolveAcrossCalls>(app,
            parameters: new[] { ("value", (object?)"%x%") });
        var firstTyped = first.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(firstTyped!.Value).IsEqualTo("first");

        app.User.Context.Variable.Set("x", "second");
        var second = await MatrixRunner.RunAsync<ReResolveAcrossCalls>(app,
            parameters: new[] { ("value", (object?)"%x%") });
        var secondTyped = second.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That(secondTyped!.Value).IsEqualTo("second");
    }

    // Shared Parameter Data — raw .Value never changes across calls.
    [Test]
    public async Task ReResolveAcrossCalls_SharedParameterData_RawValueUnchanged()
    {
        await using var app = new global::app.@this("/app");
        var sharedData = new Data("value", "%x%");

        app.User.Context.Variable.Set("x", "v1");
        var action1 = new PrAction
        {
            Module = "matrix.resolution",
            ActionName = "reresolveacrosscalls",
            Parameters = new List<Data> { sharedData }
        };
        MatrixRunner.EnsureRegistered<ReResolveAcrossCalls>(app);
        await action1.RunAsync(app.User.Context);

        // Raw .Value is still "%x%" — no in-place mutation
        await Assert.That(sharedData.Value).IsEqualTo("%x%");

        app.User.Context.Variable.Set("x", "v2");
        var action2 = new PrAction
        {
            Module = "matrix.resolution",
            ActionName = "reresolveacrosscalls",
            Parameters = new List<Data> { sharedData }
        };
        await action2.RunAsync(app.User.Context);

        await Assert.That(sharedData.Value).IsEqualTo("%x%");
    }

    // Loop scenario: same action runs N times with %i% changing each iteration → each read fresh.
    [Test]
    public async Task ReResolveAcrossCalls_LoopIteration_EachReadFresh()
    {
        await using var app = new global::app.@this("/app");
        var seen = new List<string?>();
        for (int i = 0; i < 3; i++)
        {
            app.User.Context.Variable.Set("i", $"value-{i}");
            var r = await MatrixRunner.RunAsync<ReResolveAcrossCalls>(app,
                parameters: new[] { ("value", (object?)"%i%") });
            var typed = r.Data as global::app.data.@this<global::app.type.text.@this>;
            seen.Add(typed!.Value);
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
        await using var app = new global::app.@this("/app");
        app.User.Context.Variable.Set("x", "value");

        // Pre-register; run in parallel.
        MatrixRunner.EnsureRegistered<ConcurrentHandlers>(app);
        var sharedData = new Data("value", "%x%");

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            var action = new PrAction
            {
                Module = "matrix.resolution",
                ActionName = "concurrenthandlers",
                Parameters = new List<Data> { sharedData }
            };
            var data = await action.RunAsync(app.User.Context);
            return data.Success && (data is global::app.data.@this<global::app.type.text.@this> typed) && typed.Value == "value";        })).ToArray();

        var results = await Task.WhenAll(tasks);
        await Assert.That(results.All(b => b)).IsTrue();
    }

    // Two parallel As<T> calls on the same Data → each returns independent Data<T>.
    [Test]
    public async Task ConcurrentHandlers_ParallelAsT_IndependentResults()
    {
        await using var app = new global::app.@this("/app");
        app.User.Context.Variable.Set("x", "shared");
        var data = new Data("v", "%x%") { Context = app.User.Context };

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            data.As<global::app.type.text.@this>(app.User.Context))).ToArray();
        var results = await Task.WhenAll(tasks);

        // Each should be independent and successful with the same resolved value.
        await Assert.That(results.All(r => r.Value == "shared")).IsTrue();
        // Distinct instances (no shared cache).
        var distinctCount = results.Select(r => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(r))
            .Distinct().Count();
        // At least more than 1 distinct (allow some hash collisions).
        await Assert.That(distinctCount > 1).IsTrue();
    }
}
