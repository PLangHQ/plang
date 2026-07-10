namespace PLang.Tests.App.DataTests;

// Contract tests for Data.Value<T>() — the new resolution entry point in v4 Phase 2.
// v4 contract: As<T> walks _value, substitutes %var% via context.Variable.Get/Resolve, converts to T via TypeMapping,
//   and returns a fresh Data<T>. Every call resolves freshly. Data is stateless w.r.t. resolution.

public class DataAsTResolutionTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Value is T already (boxed) but Data is not typed → As<T> wraps in fresh Data<T>.
    [Test]
    public async Task AsT_ValueAlreadyT_FastPathWrap()
    {
        var data = new Data("count", 42, context: _app.User.Context);
        var result = data.ShallowClone<global::app.type.item.number.@this>(await data.Value<global::app.type.item.number.@this>());
        await Assert.That(result).IsTypeOf<global::app.data.@this<global::app.type.item.number.@this>>();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("42");
    }

    // Value is "%name%" (full match), (await Variables.Get("name")).Value is T → returns Data<T> with that value.
    [Test]
    public async Task AsT_FullVarMatch_ReturnsVariableValue()
    {
        _app.User.Context.Variable.Set("path", "/tmp/x.txt");
        var data = new Data("p", "%path%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("/tmp/x.txt");
    }

    // Value is "%name%" but Variables doesn't have "name" → returns null/NotFound, not exception.
    [Test]
    public async Task AsT_FullVarMatch_MissingVariable_ReturnsErrorOrNotFound()
    {
        var data = new Data("p", "%missing%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        // Either Data.FromError (Success=false) or empty value — both are valid contract responses.
        await Assert.That(result).IsNotNull();
        await Assert.That((await result.Value())).IsNull();
    }

    // Value is "Hello %name%" (partial) → Variables.Resolve invoked, result cast to T.
    [Test]
    public async Task AsT_Interpolation_CallsResolve()
    {
        _app.User.Context.Variable.Set("name", "world");
        var data = new Data("greeting", "Hello %name%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello world");
    }

    // Value is List<object?> with nested %var% strings → walks list, substitutes, converts to List<T>.
    [Test]
    public async Task AsT_ListWithNestedVars_DeepResolvesAndTypes()
    {
        _app.User.Context.Variable.Set("greeting", "hello");
        var raw = new List<object?> { "%greeting%", "world" };
        var data = TemplateStamp.Container("list", raw, _app.User.Context);

        var result = data.ShallowClone<global::app.type.list.@this<global::app.type.item.text.@this>>(await data.Value<global::app.type.list.@this<global::app.type.item.text.@this>>());

        await Assert.That((await result.Value())).IsNotNull();
        var items = result.GetValue<List<string>>()!;
        await Assert.That((items[0])?.ToString()).IsEqualTo("hello");
        await Assert.That((items[1])?.ToString()).IsEqualTo("world");
    }

    // Value is Dictionary<string, object?> with %var% in values → walks, substitutes, converts.
    [Test]
    public async Task AsT_DictWithNestedVars_DeepResolvesAndTypes()
    {
        _app.User.Context.Variable.Set("prompt", "You are a compiler");
        var raw = new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%prompt%" };
        var data = TemplateStamp.Container("dict", raw, _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.dict.@this>(await data.Value<global::app.type.item.dict.@this>());

        await Assert.That((await result.Value())).IsNotNull();
        var dict = result.GetValue<Dictionary<string, object?>>()!;
        await Assert.That((dict["content"])?.ToString()).IsEqualTo("You are a compiler");
    }

    // T has static Resolve(string, Context) (e.g., FileSystem.path) → As<T> dispatches to it for string Values.
    [Test]
    public async Task AsT_TypeWithStaticResolve_StringValue_DispatchesToResolve()
    {
        var data = new Data("file", "subdir/file.txt", context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.path.@this>(await data.Value<global::app.type.item.path.@this>());

        // FileSystem.path.Resolve returned a Path instance — Value should be one.
        await Assert.That((await result.Value())).IsNotNull();
        await Assert.That((await result.Value()) is global::app.type.item.path.@this).IsTrue();
    }

    // TypeMapping conversion failure → Data.FromError with structured error.
    [Test]
    public async Task AsT_ConversionFailure_ReturnsFromError()
    {
        var data = new Data("count", "not-a-number", context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.number.@this>(await data.Value<global::app.type.item.number.@this>());

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
    }

    // Two consecutive As<T> calls with the same context → walk runs twice, two fresh Data<T> instances.
    [Test]
    public async Task AsT_CalledTwice_FreshResolutionEachCall()
    {
        _app.User.Context.Variable.Set("x", "first");
        var data = new Data("v", "%x%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var first = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());
        await Assert.That((await first.Value())?.ToString()).IsEqualTo("first");

        _app.User.Context.Variable.Set("x", "second");
        var second = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());
        await Assert.That((await second.Value())?.ToString()).IsEqualTo("second");

        // Two distinct instances — neither is a cache.
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    // After first As<T>, original Data._value is unchanged (raw preserved).
    [Test]
    public async Task AsT_DoesNotMutateOriginalDataValue()
    {
        _app.User.Context.Variable.Set("x", "resolved");
        var data = new Data("v", "%x%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var resolved = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());
        await Assert.That((await resolved.Value())?.ToString()).IsEqualTo("resolved");

        // The original is not mutated — the source form is intact (Peek never
        // renders). Value() on a stamped template renders live by design.
        await Assert.That(data.Peek()?.ToString()).IsEqualTo("%x%");
    }

    // Stored values are values, not expressions. A stored string that happens to match
    // %var% syntax is opaque payload — reading it returns the bytes verbatim, no chain
    // resolution. Matches mainstream language assignment semantics (C, Python, JS, C#).
    // No "cycle" can form because no recursion fires.
    [Test]
    public async Task AsT_StoredFullVarRef_ReturnedVerbatim_NoChain()
    {
        _app.User.Context.Variable.Set("a", "%b%");
        _app.User.Context.Variable.Set("b", "%a%");
        var data = new Data("ref", "%a%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("%b%");
    }

    // Self-referencing stored value — same rule. %x% holds the bytes "%x%"; reading
    // returns "%x%". The PLang chain that would build this state via `set` would
    // eagerly evaluate and never reach a self-loop; this test proves the read path
    // is robust even when the store is poked directly.
    [Test]
    public async Task AsT_StoredSelfRef_ReturnedVerbatim()
    {
        _app.User.Context.Variable.Set("x", "%x%");
        var data = new Data("ref", "%x%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("%x%");
    }

    // Partial-match interpolation fires once over the slot's literal text, then stops.
    // %var% references that come in via the substituted value are payload — not expressions.
    // Here %x% holds "%x%": Variables.Resolve replaces %x% with "%x%", yielding "hello %x%",
    // and the result is final.
    [Test]
    public async Task AsT_PartialMatchInterpolatesOncesThenStops()
    {
        _app.User.Context.Variable.Set("x", "%x%");
        var data = new Data("greeting", "hello %x%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("hello %x%");
    }

    // Stored value is "X-%b%" — a string with embedded %var% text. The read returns it
    // verbatim. No "expanding cycle" can form because no recursion fires on the substituted
    // bytes. (Why this matters for the builder: render-template output is exactly this
    // shape — literal source text with embedded %var% — and used to blow up the LLM prompt
    // by getting re-resolved against the builder's scope.)
    [Test]
    public async Task AsT_StoredVarRefWithSurroundingText_NotReResolved()
    {
        _app.User.Context.Variable.Set("a", "X-%b%");
        _app.User.Context.Variable.Set("b", "Y-%a%");
        var data = new Data("ref", "%a%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("X-%b%");
    }

    // Chains of stored %var% references do NOT transitively resolve. Reading %a% returns
    // its stored bytes "%b%", not the leaf value at the end of the chain. The right way
    // to capture an indirection is at write time (`set %a% = %e%` resolves through the
    // chain when assigning), not at read time.
    [Test]
    public async Task AsT_DeepChain_NoTransitiveResolution()
    {
        _app.User.Context.Variable.Set("a", "%b%");
        _app.User.Context.Variable.Set("b", "%c%");
        _app.User.Context.Variable.Set("c", "%d%");
        _app.User.Context.Variable.Set("d", "%e%");
        _app.User.Context.Variable.Set("e", "leaf-value");
        var data = new Data("chain", "%a%", new global::app.type.@this("text", null, false, "plang"), context: _app.User.Context);

        var result = data.ShallowClone<global::app.type.item.text.@this>(await data.Value<global::app.type.item.text.@this>());

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("%b%");
    }

    // Reproduces the LlmFixer 280k-prompt explosion. A typed slot reading a stored container
    // (List<Dict>) used to walk the container's leaves a second time and re-resolve embedded
    // %var% strings against the current scope — turning literal %goal% / %x% / %y% inside
    // a Content payload into the builder's actual variable values. With the fix, the stored
    // container is returned verbatim: Content keeps its literal %var% bytes.
    //
    // Setup mirrors how the builder's `set %messages% = [{Content: "%goalForLlm%"}]` lands
    // in storage (variable.set's first walk substitutes %goalForLlm% into Content). The
    // bug fires on the second read at llm.query Messages=%messages%.
    [Test]
    public async Task AsT_TypedContainerSlot_StoredLeavesNotReResolved()
    {
        var context = _app.User.Context;
        context.Variable.Set("x", "BUILDER-X");
        context.Variable.Set("y", "BUILDER-Y");

        // Mirrors what variable.set produces after walking [{Content: "%goalForLlm%"}]:
        // Content holds the rendered template's literal text, with embedded %var% as bytes.
        var stored = new List<object?>
        {
            new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["Role"] = "user",
                ["Content"] = "literal text with %x% and %y% inside"
            }
        };
        context.Variable.Set(new global::app.data.@this<global::app.type.list.@this<global::app.type.item.@this>>("messages", new global::app.type.list.@this<global::app.type.item.@this>(System.Linq.Enumerable.Select(stored, d => _app.Data("", d)), context), context: context));

        var paramData = new Data("Messages", "%messages%", new global::app.type.@this("text", null, false, "plang"), context: context);
        var result = paramData.ShallowClone<global::app.type.list.@this<global::app.type.item.dict.@this>>(await paramData.Value<global::app.type.list.@this<global::app.type.item.dict.@this>>());

        await result.IsSuccess();
        var content = (string)result.GetValue<List<Dictionary<string, object?>>>()![0]["Content"]!;
        await Assert.That(content).IsEqualTo("literal text with %x% and %y% inside");
        // Negative assertion — the bug substituted these:
        await Assert.That(content).DoesNotContain("BUILDER-X");
        await Assert.That(content).DoesNotContain("BUILDER-Y");
    }

    // Closer LlmFixer reproducer: stored as Data<global::app.type.list.@this<object?>> (the actual minted type from
    // variable.set's MintTyped path), read as List<LlmMessage> (the actual llm.query slot).
    // The conversion goes List<object?> → List<LlmMessage> per element via JSON roundtrip —
    // which should preserve literal %var%, but the actual builder run shows substitution
    // happening somewhere in this exact path. Confirms whether the leak is in conversion.
    [Test]
    public async Task AsT_ListObjectSlot_AsListLlmMessage_StoredLeavesNotReResolved()
    {
        var context = _app.User.Context;
        context.Variable.Set("goal", new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase) { ["Name"] = "BuildGoal" });
        context.Variable.Set("buildStart", 999_999_999L);

        // Mirrors what variable.set's MintTyped stores after walking the literal list.
        // Each element is a Dictionary<string, object?>, with Content carrying literal %var%.
        var stored = new List<object?>
        {
            new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["Role"] = "user",
                ["Content"] = "literal text with %goal.Name% and %buildStart% inside"
            }
        };
        context.Variable.Set(new global::app.data.@this<global::app.type.list.@this<global::app.type.item.@this>>("fixerMessages", new global::app.type.list.@this<global::app.type.item.@this>(System.Linq.Enumerable.Select(stored, d => _app.Data("", d)), context), context: context));

        // Mirrors how llm.query reads %fixerMessages% — typed slot is List<LlmMessage>.
        var paramData = new Data("Messages", "%fixerMessages%", new global::app.type.@this("text", null, false, "plang"), context: context);
        var result = paramData.ShallowClone<global::app.type.list.@this<global::app.module.llm.LlmMessage>>(await paramData.Value<global::app.type.list.@this<global::app.module.llm.LlmMessage>>());

        await result.IsSuccess();
        var content = result.GetValue<List<global::app.module.llm.LlmMessage>>()![0].Content!;
        await Assert.That(content).IsEqualTo("literal text with %goal.Name% and %buildStart% inside");
        // Negative assertion — the bug substituted these:
        await Assert.That(content).DoesNotContain("BuildGoal");
        await Assert.That(content).DoesNotContain("999999999");
    }
}
