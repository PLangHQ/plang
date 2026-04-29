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
