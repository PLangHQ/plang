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

    // Cyclic %var% reference (a → b → a) must NOT stack-overflow AND must surface a
    // structured error so callers see the failure rather than an unresolved %var% leak.
    [Test]
    public async Task AsT_CyclicVarReference_ReturnsCycleError()
    {
        _app.Context.Variables.Set("a", "%b%");
        _app.Context.Variables.Set("b", "%a%");
        var data = new Data("ref", "%a%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableResolutionCycle");
    }

    // Self-referencing %var% (x → %x%) must NOT stack-overflow AND surfaces the cycle error.
    [Test]
    public async Task AsT_SelfReferencingVar_ReturnsCycleError()
    {
        _app.Context.Variables.Set("x", "%x%");
        var data = new Data("ref", "%x%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableResolutionCycle");
    }

    // Self-reference inside an interpolation (e.g. "hello %x%" where %x% = "%x%") must NOT
    // stack-overflow AND surfaces the cycle error rather than passing through the template.
    [Test]
    public async Task AsT_PartialMatchSelfReference_ReturnsCycleError()
    {
        _app.Context.Variables.Set("x", "%x%");
        var data = new Data("greeting", "hello %x%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        // The partial-match branch interpolates "hello %x%" via Variables.Resolve, which
        // re-emits "hello %x%" (x's value is "%x%"). The recursion then trips the cycle
        // protector and surfaces a structured error.
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableResolutionCycle");
    }

    // Expanding cycle: %a%="X-%b%", %b%="Y-%a%" produces "X-%b%" → "X-Y-%a%" → "X-Y-X-%b%" → …
    // every recursion is a *new* string, so the HashSet alone never trips. Without the depth
    // bound this stack-overflows. The bound now surfaces a structured ResolveDepthExceeded
    // error rather than silently passing the half-resolved template through.
    [Test]
    public async Task AsT_ExpandingCycle_DepthBoundReturnsError()
    {
        _app.Context.Variables.Set("a", "X-%b%");
        _app.Context.Variables.Set("b", "Y-%a%");
        var data = new Data("ref", "%a%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        // Critical: it returned at all (no StackOverflowException).
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ResolveDepthExceeded");
    }

    // Non-cyclic chain of indirections still resolves end-to-end.
    [Test]
    public async Task AsT_DeepChain_5Levels_ResolvesCorrectly()
    {
        _app.Context.Variables.Set("a", "%b%");
        _app.Context.Variables.Set("b", "%c%");
        _app.Context.Variables.Set("c", "%d%");
        _app.Context.Variables.Set("d", "%e%");
        _app.Context.Variables.Set("e", "leaf-value");
        var data = new Data("chain", "%a%") { Context = _app.Context };

        var result = data.As<string>(_app.Context);

        await Assert.That(result.Value).IsEqualTo("leaf-value");
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
}
