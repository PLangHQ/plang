namespace PLang.Tests.App.DataTests;

// Phase 2b contract — As<T> preserves identity. The architect's anchor: every
// plang variable IS Data; cross-type views are LIVE windows into the same
// variable, sharing Properties + the three event lists by reference. Only Type
// and the converted .Value differ between a source and its typed view.
//
// Identity rules (architect/v1/plan.md §Phase 2):
//   1. Same-type fast path → source returned as-is, no allocation.
//   2. Variance fast path (U:T) → new global::app.data.@this<T> wrapping, .Value cast-only ref-share, state aliased.
//   3. Cross-type with conversion → new global::app.data.@this<T>, .Value converted, state aliased.
//   4. Plain Data target → no As<T> at all; canonical (live var or param Data) returned as-is.
//
// Reference-equality is the contract — tests assert ref-share where the architect
// requires it and ref-distinct where a fresh wrapper is required. Not asserting
// "exactly N allocations" so the implementation has flexibility on the inside.

public class AsTIdentityTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Rule 1 — same-type fast path. As<int>() on Data<int> returns the source
    // instance. ReferenceEquals is the only check that proves zero allocation
    // and full identity (Properties, event lists, Name, Type, everything is
    // trivially shared because it's the same object).
    [Test]
    public async Task AsT_SameType_ReturnsSourceInstance()
    {
        var source = new global::app.data.@this<int>("count", 42) { Context = _app.User.Context };
        var result = source.As<int>();
        await Assert.That(ReferenceEquals(source, result)).IsTrue();
    }

    // Trivial corollary of the same-type fast path: Properties is the same ref
    // because the instance is the same. Pinned separately to make the contract
    // explicit — a future "always wrap" optimization would break this test
    // even if it kept value equality.
    [Test]
    public async Task AsT_SameType_PreservesProperties()
    {
        var source = new global::app.data.@this<int>("count", 42) { Context = _app.User.Context };
        source.Properties.Set("meta", "abc");
        var result = source.As<int>();
        await Assert.That(ReferenceEquals(source.Properties, result.Properties)).IsTrue();
        await Assert.That(result.Properties["meta"]).IsEqualTo("abc");
    }

    // Rule 2 — variance fast path. Data<List<int>>.As<IEnumerable>() produces
    // a new global::app.data.@this<IEnumerable> instance (Type changes), but .Value is the SAME
    // List<int> reference — cast-only, no copy. Mutating the underlying list
    // through wrapped.Value is visible through source.Value.
    [Test]
    public async Task AsT_Variance_ListToIEnumerable_ValueRefShared()
    {
        var list = new List<int> { 1, 2, 3 };
        var source = new global::app.data.@this<List<int>>("nums", list) { Context = _app.User.Context };
        var wrapped = source.As<System.Collections.IEnumerable>();
        await Assert.That(ReferenceEquals(wrapped.Value, list)).IsTrue();
        // Mutate via the underlying list — wrapped sees the change.
        list.Add(4);
        var copied = new List<int>();
        foreach (int n in wrapped.Value!) copied.Add(n);
        await Assert.That(copied).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    // Variance fast path aliases Properties from source onto the wrapped Data.
    // ref-equal: Adding to source.Properties is visible via wrapped.Properties
    // because they ARE the same Properties bag.
    [Test]
    public async Task AsT_Variance_PropertiesAliased()
    {
        var list = new List<int> { 1, 2 };
        var source = new global::app.data.@this<List<int>>("nums", list) { Context = _app.User.Context };
        var wrapped = source.As<System.Collections.IEnumerable>();
        await Assert.That(ReferenceEquals(source.Properties, wrapped.Properties)).IsTrue();
        source.Properties.Set("annot", "via-source");
        await Assert.That(wrapped.Properties["annot"]).IsEqualTo("via-source");
    }

    // Variance fast path aliases all three event lists. Subscribing on either
    // side and firing on the other proves the lists are ref-shared, not copied.
    [Test]
    public async Task AsT_Variance_OnChangeAliased_FireOnSourceVisibleThroughWrapped()
    {
        var list = new List<int> { 1 };
        var source = new global::app.data.@this<List<int>>("nums", list) { Context = _app.User.Context };
        var wrapped = source.As<System.Collections.IEnumerable>();
        await Assert.That(ReferenceEquals(source.OnChange, wrapped.OnChange)).IsTrue();
        var seen = 0;
        wrapped.OnChange.Add((_, _) => seen++);
        source.FireOnChange(new global::app.data.@this<List<int>>("nums", new List<int>()));
        await Assert.That(seen).IsEqualTo(1);
    }

    // Stronger variant of the above: AFTER wrap, add a subscriber via
    // wrapped.OnChange. Verify it's reachable via source.OnChange (same list).
    // Distinguishes "copied snapshot at wrap time" from "ref-shared list" —
    // a snapshot would not see the post-wrap subscriber; a ref-share would.
    [Test]
    public async Task AsT_Variance_PostWrapSubscribe_VisibleThroughBothRefs()
    {
        var list = new List<int> { 1 };
        var source = new global::app.data.@this<List<int>>("nums", list) { Context = _app.User.Context };
        var wrapped = source.As<System.Collections.IEnumerable>();
        Action<Data, Data> handler = (_, _) => { };
        wrapped.OnChange.Add(handler);
        await Assert.That(source.OnChange).Contains(handler);
    }

    // Rule 3 — cross-type with conversion. Data<int>(42).As<string>() produces
    // a NEW Data<string> with converted .Value ("42"), but Properties + event
    // lists alias from source. The .Value is a fresh converted object —
    // ref-DISTINCT from source.Value (42 boxed) — but the metadata bag is shared.
    [Test]
    public async Task AsT_CrossType_ConversionWraps_PropertiesAliased()
    {
        var source = new global::app.data.@this<int>("count", 42) { Context = _app.User.Context };
        source.Properties.Set("note", "hello");
        var wrapped = source.As<string>();
        await Assert.That(ReferenceEquals(source, wrapped)).IsFalse();
        await Assert.That(wrapped.Value).IsEqualTo("42");
        await Assert.That(ReferenceEquals(source.Properties, wrapped.Properties)).IsTrue();
        await Assert.That(wrapped.Properties["note"]).IsEqualTo("hello");
    }

    // Conversion failure path. As<T>() on a value that can't convert to T
    // returns Data<T>.FromError(error) — a sentinel with Success=false. The
    // sentinel must NOT alias source's Properties or event lists (it's not a
    // valid view of the variable). Verifies the failure path is hermetic.
    [Test]
    public async Task AsT_CrossType_ConversionFailure_ReturnsFromError_NoAlias()
    {
        var source = new global::app.data.@this<string>("messy", "not-a-number") { Context = _app.User.Context };
        source.Properties.Set("extra", "leak-check");
        var wrapped = source.As<int>();
        await Assert.That(wrapped.Success).IsFalse();
        await Assert.That(ReferenceEquals(source.Properties, wrapped.Properties)).IsFalse();
        await Assert.That(ReferenceEquals(source.OnChange, wrapped.OnChange)).IsFalse();
    }

    // Rule 4a — plain Data target with literal parameter. AsCanonical on a Data
    // whose Value is a literal (no %) returns `this` — no wrap, no clone.
    [Test]
    public async Task AsT_PlainDataTarget_LiteralParameter_ReturnsParameterDataAsIs()
    {
        var paramData = new Data("Slot", "literal value") { Context = _app.User.Context };
        var canonical = paramData.AsCanonical();
        await Assert.That(ReferenceEquals(paramData, canonical)).IsTrue();
    }

    // Rule 4b — plain Data target with %var% reference. AsCanonical on a Data
    // whose Value is "%products%" returns the LIVE variable Data from
    // Variables.Get("products"). Mutations via the returned Data are visible
    // through Variables.Get.
    [Test]
    public async Task AsT_PlainDataTarget_VarReference_ReturnsLiveVariableData()
    {
        var context = _app.User.Context;
        var live = new global::app.data.@this<List<object?>>("products", new List<object?> { "a", "b" }) { Context = context };
        context.Variables.Set(live);

        var paramData = new Data("Slot", "%products%") { Context = context };
        var canonical = paramData.AsCanonical();

        await Assert.That(ReferenceEquals(canonical, live)).IsTrue();
        // Mutation propagates: appending via live's value is visible through Variables.Get.
        ((List<object?>)canonical.Value!).Add("c");
        var stored = (List<object?>)context.Variables.Get("products").Value!;
        await Assert.That(stored.Count).IsEqualTo(3);
    }

    // Rule 4c — plain Data target with a list whose elements contain %var% references.
    // AsCanonical must walk the list and substitute nested vars, returning a fresh Data
    // (not `this`, since the container is rewritten with resolved values).
    [Test]
    public async Task AsT_PlainDataTarget_ListWithNestedVars_ResolvesAndReturnsFreshData()
    {
        var context = _app.User.Context;
        context.Variables.Set("greeting", "hello");
        var raw = new List<object?> { "%greeting%", "literal" };
        var paramData = new Data("Slot", raw) { Context = context };

        var canonical = paramData.AsCanonical();

        await Assert.That(ReferenceEquals(canonical, paramData)).IsFalse();
        var resolved = (List<object?>)canonical.Value!;
        await Assert.That(resolved[0]).IsEqualTo("hello");
        await Assert.That(resolved[1]).IsEqualTo("literal");
    }

    // Rule 4d — plain Data target with a dict whose values contain %var% references.
    // The same uniformity rule applies: nested vars resolve regardless of whether the
    // handler property is plain Data or Data<T>.
    [Test]
    public async Task AsT_PlainDataTarget_DictWithNestedVars_ResolvesAndReturnsFreshData()
    {
        var context = _app.User.Context;
        context.Variables.Set("prompt", "You are a compiler");
        var raw = new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%prompt%" };
        var paramData = new Data("Slot", raw) { Context = context };

        var canonical = paramData.AsCanonical();

        await Assert.That(ReferenceEquals(canonical, paramData)).IsFalse();
        var resolved = (Dictionary<string, object?>)canonical.Value!;
        await Assert.That(resolved["role"]).IsEqualTo("system");
        await Assert.That(resolved["content"]).IsEqualTo("You are a compiler");
    }

    // Rule 4e — list-of-dicts with nested vars (the BuildGoalCore pattern). Verifies that
    // SubstitutePrimitive recurses into nested dicts on the AsCanonical path, matching the
    // typed AsT path. Without this fix the Content field stayed literal "%buildGoalPrompt%".
    [Test]
    public async Task AsT_PlainDataTarget_ListOfDictsWithNestedVars_DeepResolves()
    {
        var context = _app.User.Context;
        context.Variables.Set("prompt", "You are a compiler");
        context.Variables.Set("user", "build this goal");
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["Role"] = "system", ["Content"] = "%prompt%" },
            new Dictionary<string, object?> { ["Role"] = "user",   ["Content"] = "%user%" }
        };
        var paramData = new Data("messages", raw) { Context = context };

        var canonical = paramData.AsCanonical();

        var resolved = (List<object?>)canonical.Value!;
        var first = (Dictionary<string, object?>)resolved[0]!;
        var second = (Dictionary<string, object?>)resolved[1]!;
        await Assert.That(first["Content"]).IsEqualTo("You are a compiler");
        await Assert.That(second["Content"]).IsEqualTo("build this goal");
    }

    // Rule 4f — literal list (no %vars% anywhere) still walks. Symmetric with the typed
    // path: `As<T>` on a List<object?> always allocates via WalkList, and AsCanonical
    // mirrors that. Asserting on values (not ref-equality) keeps the contract focused on
    // resolution semantics, not allocation count.
    [Test]
    public async Task AsT_PlainDataTarget_LiteralList_NoNestedVars_PreservesValues()
    {
        var context = _app.User.Context;
        var raw = new List<object?> { "a", "b", "c" };
        var paramData = new Data("items", raw) { Context = context };

        var canonical = paramData.AsCanonical();

        var resolved = (List<object?>)canonical.Value!;
        await Assert.That(resolved.Count).IsEqualTo(3);
        await Assert.That(resolved[0]).IsEqualTo("a");
        await Assert.That(resolved[1]).IsEqualTo("b");
        await Assert.That(resolved[2]).IsEqualTo("c");
    }

    // Rule 4g — infrastructure %!var% references inside container leaves resolve at the
    // AsCanonical walk, same as plain %var%. Earlier dd7bf37e unconditionally skipped %!*%
    // here to keep builder runs from baking their own infra state into LLM-response .pr —
    // but the 959cdd36 fix ("stored values are values, no recursion") covers that case via
    // the As<T>/AsT_Convert short-circuit. The skip here was over-broad: it left
    // developer-authored infra refs literal at runtime. Concrete repro: HandleBuildGoalFailure
    // wrote `set %trace.buildError% = {"message": "%!error.Message%"}` and the trace JSON
    // captured the literal `"%!error.Message%"` instead of the actual error.
    [Test]
    public async Task AsT_PlainDataTarget_DictWithInfraVar_ResolvesAtCanonicalWalk()
    {
        var context = _app.User.Context;
        context.Variables.Set(new global::app.data.DynamicData("!error", () => "boom"));
        var raw = new Dictionary<string, object?> { ["message"] = "%!error%" };
        var paramData = new Data("trace.buildError", raw) { Context = context };

        var canonical = paramData.AsCanonical();

        var resolved = (Dictionary<string, object?>)canonical.Value!;
        await Assert.That(resolved["message"]).IsEqualTo("boom");
    }
}
