using PLang.Tests.App.Fixtures;
using App.modules.matrix.datawrapped;

namespace PLang.Tests.Generator.Matrix.DataWrapped;

public class DataWrappedStringTests
{
    [Test]
    public async Task DataWrappedString_FullVarMatch_ResolvesToVariableValue()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedString>(app,
            parameters: new[] { ("body", (object?)"%greeting%") },
            variables: new Dictionary<string, object?> { ["greeting"] = "hello" });
        var typed = result.Data as global::App.Data.@this<string>;
        await Assert.That(typed!.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task DataWrappedString_Interpolation_ResolvesViaResolve()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedString>(app,
            parameters: new[] { ("body", (object?)"Hello %name%!") },
            variables: new Dictionary<string, object?> { ["name"] = "world" });
        var typed = result.Data as global::App.Data.@this<string>;
        await Assert.That(typed!.Value).IsEqualTo("Hello world!");
    }

    [Test]
    public async Task DataWrappedString_MissingVariable_HandlesGracefully()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedString>(app,
            parameters: new[] { ("body", (object?)"%not_set%") });
        // Either FromError or null Value — both are valid; just don't crash.
        await Assert.That(result.Data).IsNotNull();
    }
}

public class DataWrappedListTests
{
    [Test]
    public async Task DataWrappedList_NestedVarInDict_DeepResolvesAndTypes()
    {
        await using var app = new global::App.@this("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%comment%" }
        };
        var result = await MatrixRunner.RunAsync<DataWrappedList>(app,
            parameters: new[] { ("messages", (object?)raw) },
            variables: new Dictionary<string, object?> { ["comment"] = "you are a compiler" });
        var typed = result.Data as global::App.Data.@this<List<global::App.modules.llm.LlmMessage>>;
        await Assert.That(typed!.Value).IsNotNull();
        await Assert.That(typed.Value![0].Content).IsEqualTo("you are a compiler");
    }

    [Test]
    public async Task DataWrappedList_EmptyList_ReturnsEmptyTyped()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedList>(app,
            parameters: new[] { ("messages", (object?)new List<object?>()) });
        var typed = result.Data as global::App.Data.@this<List<global::App.modules.llm.LlmMessage>>;
        await Assert.That(typed!.Value!.Count).IsEqualTo(0);
    }
}

public class DataWrappedDictTests
{
    [Test]
    public async Task DataWrappedDict_NestedVar_DeepResolves()
    {
        await using var app = new global::App.@this("/app");
        var raw = new Dictionary<string, object?> { ["inner"] = "%x%", ["other"] = "literal" };
        var result = await MatrixRunner.RunAsync<DataWrappedDict>(app,
            parameters: new[] { ("headers", (object?)raw) },
            variables: new Dictionary<string, object?> { ["x"] = "substituted" });
        var typed = result.Data as global::App.Data.@this<Dictionary<string, object?>>;
        await Assert.That(typed!.Value!["inner"]).IsEqualTo("substituted");
        await Assert.That(typed.Value["other"]).IsEqualTo("literal");
    }
}

public class DataWrappedActionListTests
{
    [Test]
    public async Task DataWrappedActionList_DoesNotRecurseIntoActions()
    {
        await using var app = new global::App.@this("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["module"] = "variable",
                ["action"] = "set",
                ["parameters"] = new List<Data> { new Data("v", "%comment%") }
            }
        };
        var result = await MatrixRunner.RunAsync<DataWrappedActionList>(app,
            parameters: new[] { ("actions", (object?)raw) },
            variables: new Dictionary<string, object?> { ["comment"] = "should-not-resolve" });

        var typed = result.Data as global::App.Data.@this<List<PrAction>>;
        await Assert.That(typed!.Value).IsNotNull();
        // The sub-action's parameter Value is still raw "%comment%" — not resolved.
        var subParam = typed.Value![0].Parameters?.FirstOrDefault(p => p.Name == "v");
        await Assert.That(subParam!.Value).IsEqualTo("%comment%");
    }

    [Test]
    public async Task DataWrappedActionList_SubActionParametersRemainRaw()
    {
        // Same scenario as above, asserting raw value preservation more explicitly.
        await using var app = new global::App.@this("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["module"] = "variable",
                ["action"] = "set",
                ["parameters"] = new List<Data> { new Data("a", "%x%") }
            }
        };
        var result = await MatrixRunner.RunAsync<DataWrappedActionList>(app,
            parameters: new[] { ("actions", (object?)raw) },
            variables: new Dictionary<string, object?> { ["x"] = "premature-resolution-would-be-bad" });

        var typed = result.Data as global::App.Data.@this<List<PrAction>>;
        var subParam = typed!.Value![0].Parameters?.FirstOrDefault(p => p.Name == "a");
        await Assert.That(subParam!.Value).IsEqualTo("%x%");
    }
}

// Pins the contract that closes auditor/v1 finding #1: when a Data<T> property's resolution
// fails (cycle / depth-trip → ServiceError), the error MUST surface to the caller — even when
// the handler reads .Value instead of returning the wrapper directly. The pre-fix behavior
// was that FromError-Data lived silently on the backing field and Run() proceeded with
// .Value=default(T), producing a misleading success result.
public class DataWrappedStringUsesCycleTests
{
    [Test]
    public async Task DataWrappedStringUses_CyclicVarReference_HandlerSurfacesCycleServiceError()
    {
        await using var app = new global::App.@this("/app");
        app.Context.Variables.Set("a", "%b%");
        app.Context.Variables.Set("b", "%a%");

        var result = await MatrixRunner.RunAsync<DataWrappedStringUses>(app,
            parameters: new[] { ("body", (object?)"%a%") });

        // Without the post-Run __resolutionError check, Body.Value is null,
        // len=0, and result.Data would be Ok(0). With the fix, the cycle error
        // captured during property access surfaces as the handler's return.
        await Assert.That(result.Data.Success).IsFalse();
        var error = result.Data.Error as global::App.Errors.ServiceError;
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("VariableResolutionCycle");
    }

    [Test]
    public async Task DataWrappedStringUses_ExpandingCycle_HandlerSurfacesDepthServiceError()
    {
        await using var app = new global::App.@this("/app");
        app.Context.Variables.Set("a", "X-%b%");
        app.Context.Variables.Set("b", "Y-%a%");

        var result = await MatrixRunner.RunAsync<DataWrappedStringUses>(app,
            parameters: new[] { ("body", (object?)"%a%") });

        await Assert.That(result.Data.Success).IsFalse();
        var error = result.Data.Error as global::App.Errors.ServiceError;
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ResolveDepthExceeded");
    }

    [Test]
    public async Task DataWrappedStringUses_NormalResolution_PostRunCheckIsNoOp()
    {
        // Negative test: success path is unaffected by the post-Run __resolutionError check.
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedStringUses>(app,
            parameters: new[] { ("body", (object?)"%greeting%") },
            variables: new Dictionary<string, object?> { ["greeting"] = "hello" });

        await Assert.That(result.Data.Success).IsTrue();
        // Run() returns Data.Ok(int) — base Data with boxed int, not Data<int>.
        await Assert.That(result.Data.Value).IsEqualTo(5);
    }
}
