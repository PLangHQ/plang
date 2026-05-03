namespace PLang.Tests.App.DataTests;

// Contract tests for Data.As<T>(context) — the new resolution entry point in v4 Phase 2.
// v4 contract: As<T> walks _value, substitutes %var% via context.Variables.Get/Resolve, converts to T via TypeMapping,
//   and returns a fresh Data<T>. Every call resolves freshly. Data is stateless w.r.t. resolution.

public class DataAsTResolutionTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::App.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // this is Data<T> with correct typed Value already → As<T> returns this (fast path, no allocation).
    [Test]
    public async Task AsT_AlreadyTypedData_ReturnsSelf()
    {
        var typed = new global::App.Data.@this<int>("count", 42) { Context = _app.Context };
        var result = typed.As<int>(_app.Context);
        await Assert.That(ReferenceEquals(result, typed)).IsTrue();
    }

    // Value is T already (boxed) but Data is not typed → As<T> wraps in fresh Data<T>.
    [Test]
    public async Task AsT_ValueAlreadyT_FastPathWrap()
    {
        var data = new Data("count", 42) { Context = _app.Context };
        var result = data.As<int>(_app.Context);
        await Assert.That(result).IsTypeOf<global::App.Data.@this<int>>();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    // Value is "%name%" (full match), Variables.Get("name").Value is T → returns Data<T> with that value.
    [Test]
    public async Task AsT_FullVarMatch_ReturnsVariableValue()
    {
        _app.Context.Variables.Set("path", "/tmp/x.txt");
        var data = new Data("p", "%path%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("/tmp/x.txt");
    }

    // Value is "%name%" but Variables doesn't have "name" → returns null/NotFound, not exception.
    [Test]
    public async Task AsT_FullVarMatch_MissingVariable_ReturnsErrorOrNotFound()
    {
        var data = new Data("p", "%missing%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        // Either Data.FromError (Success=false) or empty value — both are valid contract responses.
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value).IsNull();
    }

    // Value is "Hello %name%" (partial) → Variables.Resolve invoked, result cast to T.
    [Test]
    public async Task AsT_Interpolation_CallsResolve()
    {
        _app.Context.Variables.Set("name", "world");
        var data = new Data("greeting", "Hello %name%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Value).IsEqualTo("Hello world");
    }

    // Value is List<object?> with nested %var% strings → walks list, substitutes, converts to List<T>.
    [Test]
    public async Task AsT_ListWithNestedVars_DeepResolvesAndTypes()
    {
        _app.Context.Variables.Set("greeting", "hello");
        var raw = new List<object?> { "%greeting%", "world" };
        var data = new Data("list", raw) { Context = _app.Context };

        var result = data.As<List<string>>(_app.Context);

        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value![0]).IsEqualTo("hello");
        await Assert.That(result.Value[1]).IsEqualTo("world");
    }

    // Value is Dictionary<string, object?> with %var% in values → walks, substitutes, converts.
    [Test]
    public async Task AsT_DictWithNestedVars_DeepResolvesAndTypes()
    {
        _app.Context.Variables.Set("prompt", "You are a compiler");
        var raw = new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%prompt%" };
        var data = new Data("dict", raw) { Context = _app.Context };

        var result = data.As<Dictionary<string, object?>>(_app.Context);

        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value!["content"]).IsEqualTo("You are a compiler");
    }

    // T has static Resolve(string, Context) (e.g., FileSystem.Path) → As<T> dispatches to it for string Values.
    [Test]
    public async Task AsT_TypeWithStaticResolve_StringValue_DispatchesToResolve()
    {
        var data = new Data("file", "subdir/file.txt") { Context = _app.Context };

        var result = data.As<global::App.FileSystem.Path>(_app.Context);

        // FileSystem.Path.Resolve returned a Path instance — Value should be one.
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value).IsTypeOf<global::App.FileSystem.Path>();
    }

    // TypeMapping conversion failure → Data.FromError with structured error.
    [Test]
    public async Task AsT_ConversionFailure_ReturnsFromError()
    {
        var data = new Data("count", "not-a-number") { Context = _app.Context };

        var result = data.As<int>(_app.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
    }

    // Two consecutive As<T> calls with the same context → walk runs twice, two fresh Data<T> instances.
    [Test]
    public async Task AsT_CalledTwice_FreshResolutionEachCall()
    {
        _app.Context.Variables.Set("x", "first");
        var data = new Data("v", "%x%") { Context = _app.Context };

        var first = data.As<string>(_app.Context);
        await Assert.That(first.Value).IsEqualTo("first");

        _app.Context.Variables.Set("x", "second");
        var second = data.As<string>(_app.Context);
        await Assert.That(second.Value).IsEqualTo("second");

        // Two distinct instances — neither is a cache.
        await Assert.That(ReferenceEquals(first, second)).IsFalse();
    }

    // After first As<T>, original Data._value is unchanged (raw preserved).
    [Test]
    public async Task AsT_DoesNotMutateOriginalDataValue()
    {
        _app.Context.Variables.Set("x", "resolved");
        var data = new Data("v", "%x%") { Context = _app.Context };

        var resolved = data.As<string>(_app.Context);
        await Assert.That(resolved.Value).IsEqualTo("resolved");

        // Original .Value is still raw.
        await Assert.That(data.Value).IsEqualTo("%x%");
    }

    // List<Action.@this> elements pass through As<T> WITHOUT walking into Action templates.
    [Test]
    public async Task AsT_ActionListElements_NotRecursedInto()
    {
        _app.Context.Variables.Set("comment", "should-NOT-substitute");
        // A list of action-template-shaped dictionaries; sub-actions hold raw %var% for deferred resolution.
        var raw = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["module"] = "variable",
                ["action"] = "set",
                ["parameters"] = new List<Data>
                {
                    new("comment", "%comment%")
                }
            }
        };
        var data = new Data("actions", raw) { Context = _app.Context };

        var result = data.As<List<PrAction>>(_app.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        // The substituted value should NOT have appeared inside the Action template — the raw %comment% remains.
        var firstAction = result.Value![0];
        var commentParam = firstAction.Parameters?.FirstOrDefault(p => p.Name == "comment");
        await Assert.That(commentParam).IsNotNull();
        await Assert.That(commentParam!.Value).IsEqualTo("%comment%");
    }

    // Non-generic IList (ArrayList) doesn't match the typed shape — passes through without
    // %var% substitution. Pinning current behavior; JSON ingestion normalizes to typed forms,
    // so production never feeds raw ArrayLists into resolution.
    [Test]
    public async Task AsT_NonGenericArrayList_PassesThroughWithoutSubstitution()
    {
        _app.Context.Variables.Set("x", "substituted");
        var raw = new System.Collections.ArrayList { "%x%", "literal" };
        var data = new Data("list", raw) { Context = _app.Context };

        // Asks for object back so the conversion doesn't try to coerce ArrayList to anything.
        var result = data.As<object>(_app.Context);

        await Assert.That(result.Value).IsEqualTo(raw);
        // Raw element [0] is still "%x%" — no walk happened.
        await Assert.That(((System.Collections.ArrayList)result.Value!)[0]).IsEqualTo("%x%");
    }

    // Non-generic IDictionary (Hashtable) — same shape contract as ArrayList above.
    [Test]
    public async Task AsT_NonGenericHashtable_PassesThroughWithoutSubstitution()
    {
        _app.Context.Variables.Set("x", "substituted");
        var raw = new System.Collections.Hashtable { ["key"] = "%x%" };
        var data = new Data("dict", raw) { Context = _app.Context };

        var result = data.As<object>(_app.Context);

        await Assert.That(((System.Collections.Hashtable)result.Value!)["key"]).IsEqualTo("%x%");
    }

    // Stored values are values, not expressions. A stored string that happens to match
    // %var% syntax is opaque payload — reading it returns the bytes verbatim, no chain
    // resolution. Matches mainstream language assignment semantics (C, Python, JS, C#).
    // No "cycle" can form because no recursion fires.
    [Test]
    public async Task AsT_StoredFullVarRef_ReturnedVerbatim_NoChain()
    {
        _app.Context.Variables.Set("a", "%b%");
        _app.Context.Variables.Set("b", "%a%");
        var data = new Data("ref", "%a%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("%b%");
    }

    // Self-referencing stored value — same rule. %x% holds the bytes "%x%"; reading
    // returns "%x%". The PLang chain that would build this state via `set` would
    // eagerly evaluate and never reach a self-loop; this test proves the read path
    // is robust even when the store is poked directly.
    [Test]
    public async Task AsT_StoredSelfRef_ReturnedVerbatim()
    {
        _app.Context.Variables.Set("x", "%x%");
        var data = new Data("ref", "%x%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("%x%");
    }

    // Partial-match interpolation fires once over the slot's literal text, then stops.
    // %var% references that come in via the substituted value are payload — not expressions.
    // Here %x% holds "%x%": Variables.Resolve replaces %x% with "%x%", yielding "hello %x%",
    // and the result is final.
    [Test]
    public async Task AsT_PartialMatchInterpolatesOncesThenStops()
    {
        _app.Context.Variables.Set("x", "%x%");
        var data = new Data("greeting", "hello %x%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("hello %x%");
    }

    // Stored value is "X-%b%" — a string with embedded %var% text. The read returns it
    // verbatim. No "expanding cycle" can form because no recursion fires on the substituted
    // bytes. (Why this matters for the builder: render-template output is exactly this
    // shape — literal source text with embedded %var% — and used to blow up the LLM prompt
    // by getting re-resolved against the builder's scope.)
    [Test]
    public async Task AsT_StoredVarRefWithSurroundingText_NotReResolved()
    {
        _app.Context.Variables.Set("a", "X-%b%");
        _app.Context.Variables.Set("b", "Y-%a%");
        var data = new Data("ref", "%a%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("X-%b%");
    }

    // Chains of stored %var% references do NOT transitively resolve. Reading %a% returns
    // its stored bytes "%b%", not the leaf value at the end of the chain. The right way
    // to capture an indirection is at write time (`set %a% = %e%` resolves through the
    // chain when assigning), not at read time.
    [Test]
    public async Task AsT_DeepChain_NoTransitiveResolution()
    {
        _app.Context.Variables.Set("a", "%b%");
        _app.Context.Variables.Set("b", "%c%");
        _app.Context.Variables.Set("c", "%d%");
        _app.Context.Variables.Set("d", "%e%");
        _app.Context.Variables.Set("e", "leaf-value");
        var data = new Data("chain", "%a%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("%b%");
    }

    // Fresh context with different variable values → As<T> picks up the new values, no stale cache.
    [Test]
    public async Task AsT_DifferentContext_PicksUpFreshVariableValues()
    {
        var data = new Data("v", "%x%");

        await using var app2 = new global::App.@this("/app2");
        app2.Context.Variables.Set("x", "from-app2");

        var result = data.As<string>(app2.Context);
        await Assert.That(result.Value).IsEqualTo("from-app2");
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
        var ctx = _app.Context;
        ctx.Variables.Set("x", "BUILDER-X");
        ctx.Variables.Set("y", "BUILDER-Y");

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
        ctx.Variables.Set(new global::App.Data.@this<List<object?>>("messages", stored) { Context = ctx });

        var paramData = new Data("Messages", "%messages%") { Context = ctx };
        var result = paramData.As<List<Dictionary<string, object?>>>(ctx);

        await Assert.That(result.Success).IsTrue();
        var content = (string)result.Value![0]["Content"]!;
        await Assert.That(content).IsEqualTo("literal text with %x% and %y% inside");
        // Negative assertion — the bug substituted these:
        await Assert.That(content).DoesNotContain("BUILDER-X");
        await Assert.That(content).DoesNotContain("BUILDER-Y");
    }
}
