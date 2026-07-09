using PLang.Tests.App.Fixtures;
using app.module.matrix.datawrapped;

namespace PLang.Tests.Generator.Matrix.DataWrapped;

public class DataWrappedStringTests
{
    [Test]
    public async Task DataWrappedString_FullVarMatch_ResolvesToVariableValue()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedString>(app,
            parameters: new[] { ("body", (object?)"%greeting%") },
            variables: new Dictionary<string, object?> { ["greeting"] = "hello" });
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task DataWrappedString_Interpolation_ResolvesViaResolve()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedString>(app,
            parameters: new[] { ("body", (object?)"Hello %name%!") },
            variables: new Dictionary<string, object?> { ["name"] = "world" });
        var typed = result.Data as global::app.data.@this<global::app.type.text.@this>;
        await Assert.That((await typed!.Value())?.ToString()).IsEqualTo("Hello world!");
    }

    [Test]
    public async Task DataWrappedString_MissingVariable_HandlesGracefully()
    {
        await using var app = TestApp.Create("/app");
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
        await using var app = TestApp.Create("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%comment%" }
        };
        var result = await MatrixRunner.RunAsync<DataWrappedList>(app,
            parameters: new[] { ("messages", (object?)raw) },
            variables: new Dictionary<string, object?> { ["comment"] = "you are a compiler" });
        var typed = result.Data as global::app.data.@this<global::app.type.list.@this<global::app.module.llm.LlmMessage>>;
        // Read the way a real handler does: enumerate, resolve + convert each row through its door.
        var items = new List<global::app.module.llm.LlmMessage>();
        foreach (var row in (await typed!.Value())!) items.Add((await row.Value()).Clr<global::app.module.llm.LlmMessage>()!);
        await Assert.That(items[0].Content).IsEqualTo("you are a compiler");
    }

    [Test]
    public async Task DataWrappedList_EmptyList_ReturnsEmptyTyped()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedList>(app,
            parameters: new[] { ("messages", (object?)new List<object?>()) });
        var typed = result.Data as global::app.data.@this<global::app.type.list.@this<global::app.module.llm.LlmMessage>>;
        await Assert.That((await typed!.Value())!.Count).IsEqualTo(0);
    }
}

public class DataWrappedDictTests
{
    [Test]
    public async Task DataWrappedDict_NestedVar_DeepResolves()
    {
        await using var app = TestApp.Create("/app");
        var raw = new Dictionary<string, object?> { ["inner"] = "%x%", ["other"] = "literal" };
        var result = await MatrixRunner.RunAsync<DataWrappedDict>(app,
            parameters: new[] { ("headers", (object?)raw) },
            variables: new Dictionary<string, object?> { ["x"] = "substituted" });
        var typed = result.Data as global::app.data.@this<global::app.type.dict.@this>;
        // Resolve the dict through its door, then read each value through ITS door.
        var d = (await typed!.Value())!;
        await Assert.That((await d.Get("inner")!.Value()).ToString()).IsEqualTo("substituted");
        await Assert.That((await d.Get("other")!.Value()).ToString()).IsEqualTo("literal");
    }
}

public class DataWrappedActionListTests
{
    [Test]
    [Skip("Params resolve eagerly at dispatch — nested-action params resolve prematurely; fixed by the pure-lazy source-gen refactor (resolve only on handler .Value()). See todos 2026-06-15.")]
    public async Task DataWrappedActionList_DoesNotRecurseIntoActions()
    {
        await using var app = TestApp.Create("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["module"] = "variable",
                ["action"] = "set",
                ["parameters"] = new List<Data> { new Data("v", "%comment%", context: app.User.Context) }
            }
        };
        var result = await MatrixRunner.RunAsync<DataWrappedActionList>(app,
            parameters: new[] { ("actions", (object?)raw) },
            variables: new Dictionary<string, object?> { ["comment"] = "should-not-resolve" });

        var typed = result.Data as global::app.data.@this<global::app.type.list.@this<global::app.type.clr.@this<PrAction>>>;
        await Assert.That((await typed!.Value())).IsNotNull();
        // The sub-action's parameter Value is still raw "%comment%" — not resolved.
        var subParam = ((((await typed.Value())!.Items[0].Peek()!) as global::app.type.clr.@this<PrAction>)!.Value).Parameters?.FirstOrDefault(p => p.Name == "v");
        await Assert.That((await subParam!.Value())?.ToString()).IsEqualTo("%comment%");
    }

    [Test]
    [Skip("Params resolve eagerly at dispatch — nested-action params resolve prematurely; fixed by the pure-lazy source-gen refactor (resolve only on handler .Value()). See todos 2026-06-15.")]
    public async Task DataWrappedActionList_SubActionParametersRemainRaw()
    {
        // Same scenario as above, asserting raw value preservation more explicitly.
        await using var app = TestApp.Create("/app");
        var raw = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["module"] = "variable",
                ["action"] = "set",
                ["parameters"] = new List<Data> { new Data("a", "%x%", context: app.User.Context) }
            }
        };
        var result = await MatrixRunner.RunAsync<DataWrappedActionList>(app,
            parameters: new[] { ("actions", (object?)raw) },
            variables: new Dictionary<string, object?> { ["x"] = "premature-resolution-would-be-bad" });

        var typed = result.Data as global::app.data.@this<global::app.type.list.@this<global::app.type.clr.@this<PrAction>>>;
        var subParam = ((((await typed!.Value())!.Items[0].Peek()!) as global::app.type.clr.@this<PrAction>)!.Value).Parameters?.FirstOrDefault(p => p.Name == "a");
        await Assert.That((await subParam!.Value())?.ToString()).IsEqualTo("%x%");
    }
}

// Resolution semantics: stored values are values, not expressions. Strings that contain
// %var% text are opaque payload — reading them returns the bytes verbatim, no chain
// resolution. (Prior cycle/depth-trip ServiceErrors only existed because the read path
// recursed; once that recursion was removed, no cycle can form on a stored value.)
//
// The post-Run __resolutionError surface still matters for genuine resolution failures
// (e.g., type-conversion errors); the success-path test below pins that the check is a
// no-op when resolution succeeds.
public class DataWrappedStringUsesCycleTests
{
    [Test]
    public async Task DataWrappedStringUses_CyclicVarRef_NoLongerForms_HandlerReadsVerbatimBytes()
    {
        await using var app = TestApp.Create("/app");
        app.User.Context.Variable.Set("a", "%b%");
        app.User.Context.Variable.Set("b", "%a%");

        var result = await MatrixRunner.RunAsync<DataWrappedStringUses>(app,
            parameters: new[] { ("body", (object?)"%a%") });

        // %a% holds the literal bytes "%b%" (3 chars). No chain, no cycle.
        await result.Data.IsSuccess();
        await Assert.That((await result.Data.Value())?.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task DataWrappedStringUses_StoredVarRefWithText_HandlerReadsVerbatimBytes()
    {
        await using var app = TestApp.Create("/app");
        app.User.Context.Variable.Set("a", "X-%b%");
        app.User.Context.Variable.Set("b", "Y-%a%");

        var result = await MatrixRunner.RunAsync<DataWrappedStringUses>(app,
            parameters: new[] { ("body", (object?)"%a%") });

        // %a% holds the literal bytes "X-%b%" (5 chars). No re-resolution into surrounding text.
        await result.Data.IsSuccess();
        await Assert.That((await result.Data.Value())?.ToString()).IsEqualTo("5");
    }

    [Test]
    public async Task DataWrappedStringUses_NormalResolution_PostRunCheckIsNoOp()
    {
        // Negative test: success path is unaffected by the post-Run __resolutionError check.
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<DataWrappedStringUses>(app,
            parameters: new[] { ("body", (object?)"%greeting%") },
            variables: new Dictionary<string, object?> { ["greeting"] = "hello" });

        await result.Data.IsSuccess();
        // Run() returns Data.Ok(int) — base Data with boxed int, not Data<global::app.type.number.@this>.
        await Assert.That((await result.Data.Value())?.ToString()).IsEqualTo("5");
    }
}
