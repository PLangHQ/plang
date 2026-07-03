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
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Rule 1 — same-type fast path. As<int>() on Data<global::app.type.number.@this> returns the source
    // instance. ReferenceEquals is the only check that proves zero allocation
    // and full identity (Properties, event lists, Name, Type, everything is
    // trivially shared because it's the same object).
    // Same-type ask is pure pass-through: the typed ask answers the source's own
    // value instance, no conversion and no allocation.
    [Test]
    public async Task AsT_SameType_ReturnsSourceInstance()
    {
        var source = new global::app.data.@this<global::app.type.number.@this>("count", 42, context: _app.User.Context);
        var result = await source.Value<global::app.type.number.@this>();
        await Assert.That(ReferenceEquals(source.Peek(), result)).IsTrue();
    }

    // Trivial corollary of the same-type fast path: Properties is the same ref
    // because the instance is the same. Pinned separately to make the contract
    // explicit — a future "always wrap" optimization would break this test
    // even if it kept value equality.
    [Test]
    public async Task AsT_SameType_PreservesProperties()
    {
        var source = new global::app.data.@this<global::app.type.number.@this>("count", 42, context: _app.User.Context);
        source.Properties.Set("meta", "abc");
        var result = source.ShallowClone<global::app.type.number.@this>(await source.Value<global::app.type.number.@this>());
        await Assert.That(ReferenceEquals(source.Properties, result.Properties)).IsTrue();
        await Assert.That(((await result.Properties.Value("meta")))?.ToString()).IsEqualTo("abc");
    }

    // Rule 2 — variance fast path. Data<number>.Value<item>() (to the base item type) produces
    // a new Data<item> instance (Type changes), but .Value is the SAME number.@this reference
    // — cast-only, no copy. (A native list/dict value is a walkable container, so As<T> walks
    // it for nested-var resolution and returns a fresh list — the cast-only ref-share applies
    // to leaf values, which are the ones that never need walking.)
    [Test]
    public async Task AsT_Variance_ScalarToItem_ValueRefShared()
    {
        var inner = (global::app.type.number.@this)42;
        var source = new global::app.data.@this<global::app.type.number.@this>("n", inner, context: _app.User.Context);
        var wrapped = source.ShallowClone<global::app.type.item.@this>(await source.Value<global::app.type.item.@this>());
        await Assert.That(ReferenceEquals(source, wrapped)).IsFalse();
        await Assert.That(ReferenceEquals((await wrapped.Value()), inner)).IsTrue();
    }

    // Variance fast path aliases Properties from source onto the wrapped Data.
    // ref-equal: Adding to source.Properties is visible via wrapped.Properties
    // because they ARE the same Properties bag.
    [Test]
    public async Task AsT_Variance_PropertiesAliased()
    {
        var inner = new global::app.type.list.@this<global::app.type.number.@this>(new[] { _app.Data("", 1), _app.Data("", 2) });
        var source = new global::app.data.@this<global::app.type.list.@this<global::app.type.number.@this>>("nums", inner, context: _app.User.Context);
        var wrapped = source.ShallowClone<global::app.type.list.@this>(await source.Value<global::app.type.list.@this>());
        await Assert.That(ReferenceEquals(source.Properties, wrapped.Properties)).IsTrue();
        source.Properties.Set("annot", "via-source");
        await Assert.That(((await wrapped.Properties.Value("annot")))?.ToString()).IsEqualTo("via-source");
    }

    // Variance fast path aliases all three event lists. Subscribing on either
    // side and firing on the other proves the lists are ref-shared, not copied.
    [Test]
    public async Task AsT_Variance_OnChangeAliased_FireOnSourceVisibleThroughWrapped()
    {
        var inner = new global::app.type.list.@this<global::app.type.number.@this>(new[] { _app.Data("", 1) });
        var source = new global::app.data.@this<global::app.type.list.@this<global::app.type.number.@this>>("nums", inner, context: _app.User.Context);
        var wrapped = source.ShallowClone<global::app.type.list.@this>(await source.Value<global::app.type.list.@this>());
        await Assert.That(ReferenceEquals(source.OnChange, wrapped.OnChange)).IsTrue();
        var seen = 0;
        wrapped.OnChange.Add((_, _) => seen++);
        source.FireOnChange(new global::app.data.@this("nums", new global::app.type.list.@this { Context = _app.User.Context }));
        await Assert.That(seen).IsEqualTo(1);
    }

    // Stronger variant of the above: AFTER wrap, add a subscriber via
    // wrapped.OnChange. Verify it's reachable via source.OnChange (same list).
    // Distinguishes "copied snapshot at wrap time" from "ref-shared list" —
    // a snapshot would not see the post-wrap subscriber; a ref-share would.
    [Test]
    public async Task AsT_Variance_PostWrapSubscribe_VisibleThroughBothRefs()
    {
        var inner = new global::app.type.list.@this<global::app.type.number.@this>(new[] { _app.Data("", 1) });
        var source = new global::app.data.@this<global::app.type.list.@this<global::app.type.number.@this>>("nums", inner, context: _app.User.Context);
        var wrapped = source.ShallowClone<global::app.type.list.@this>(await source.Value<global::app.type.list.@this>());
        Action<Data, Data> handler = (_, _) => { };
        wrapped.OnChange.Add(handler);
        await Assert.That(source.OnChange).Contains(handler);
    }

    // Rule 3 — cross-type with conversion. Data<global::app.type.number.@this>(42).Value<global::app.type.text.@this>() produces
    // a NEW Data<global::app.type.text.@this> with converted .Value ("42"), but Properties + event
    // lists alias from source. The .Value is a fresh converted object —
    // ref-DISTINCT from source.Value (42 boxed) — but the metadata bag is shared.
    [Test]
    public async Task AsT_CrossType_ConversionWraps_PropertiesAliased()
    {
        var source = new global::app.data.@this<global::app.type.number.@this>("count", 42, context: _app.User.Context);
        source.Properties.Set("note", "hello");
        var wrapped = source.ShallowClone<global::app.type.text.@this>(await source.Value<global::app.type.text.@this>());
        await Assert.That(ReferenceEquals(source, wrapped)).IsFalse();
        await Assert.That((await wrapped.Value())?.ToString()).IsEqualTo("42");
        await Assert.That(ReferenceEquals(source.Properties, wrapped.Properties)).IsTrue();
        await Assert.That(((await wrapped.Properties.Value("note")))?.ToString()).IsEqualTo("hello");
    }

    // Conversion failure path. The typed ask on a value that can't convert to T
    // answers null and lands the decline on the asking binding (source.Success
    // becomes false) — the error rides the binding the caller already holds, no
    // separate sentinel is minted.
    [Test]
    public async Task AsT_CrossType_ConversionFailure_DeclinesOnSource()
    {
        var source = new global::app.data.@this<global::app.type.text.@this>("messy", "not-a-number", context: _app.User.Context);
        var result = await source.Value<global::app.type.number.@this>();
        await Assert.That(result).IsNull();
        await source.IsFailure();
    }

    // Rule 4a — plain Data target with literal parameter. AsCanonical on a Data
    // whose Value is a literal (no %) returns `this` — no wrap, no clone.
    [Test]
    public async Task AsT_PlainDataTarget_LiteralParameter_ReturnsParameterDataAsIs()
    {
        var paramData = new Data("Slot", "literal value", context: _app.User.Context);
        var canonical = await paramData.AsCanonical();
        await Assert.That(ReferenceEquals(paramData, canonical)).IsTrue();
    }

    // Rule 4b — plain Data target with %var% reference. AsCanonical on a Data
    // whose Value is "%products%" returns the LIVE variable Data from
    // (await Variables.Get("products")). Mutations via the returned Data are visible
    // through Variables.Get.
    [Test]
    public async Task AsT_PlainDataTarget_VarReference_ReturnsLiveVariableData()
    {
        var context = _app.User.Context;
        var live = new global::app.data.@this("products", global::app.type.list.@this.FromRaw(new List<object?> { "a", "b" }, context), context: context);
        context.Variable.Set(live);

        var paramData = new Data("Slot", "%products%", new global::app.type.@this("text", null, false, "plang"), context: context);
        var canonical = await paramData.AsCanonical();

        await Assert.That(ReferenceEquals(canonical, live)).IsTrue();
        // Mutation propagates: appending via live's value is visible through Variables.Get.
        ((global::app.type.list.@this)(await canonical.Value())!).Add(_app.Data("", "c"));
        var stored = (global::app.type.list.@this)(await (await context.Variable.Get("products")).Value())!;
        await Assert.That(stored.Count).IsEqualTo(3);
    }

    // Rule 4c — plain Data target with a list whose elements contain %var% references.
    // AsCanonical must walk the list and substitute nested vars, returning a fresh Data
    // (not `this`, since the container is rewritten with resolved values).
    [Test]
    public async Task AsT_PlainDataTarget_ListWithNestedVars_ResolvesAndReturnsFreshData()
    {
        var context = _app.User.Context;
        context.Variable.Set("greeting", "hello");
        var raw = new List<object?> { "%greeting%", "literal" };
        var paramData = TemplateStamp.Container("Slot", raw, context);

        var canonical = await paramData.AsCanonical();

        await Assert.That(ReferenceEquals(canonical, paramData)).IsFalse();
        var resolved = global::app.type.item.@this.Lower<List<object?>>(await canonical.Value())!;
        await Assert.That((resolved[0])?.ToString()).IsEqualTo("hello");
        await Assert.That((resolved[1])?.ToString()).IsEqualTo("literal");
    }

    // Rule 4d — plain Data target with a dict whose values contain %var% references.
    // The same uniformity rule applies: nested vars resolve regardless of whether the
    // handler property is plain Data or Data<T>.
    [Test]
    public async Task AsT_PlainDataTarget_DictWithNestedVars_ResolvesAndReturnsFreshData()
    {
        var context = _app.User.Context;
        context.Variable.Set("prompt", "You are a compiler");
        var raw = new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%prompt%" };
        var paramData = TemplateStamp.Container("Slot", raw, context);

        var canonical = await paramData.AsCanonical();

        await Assert.That(ReferenceEquals(canonical, paramData)).IsFalse();
        var resolved = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await canonical.Value())!;
        await Assert.That((resolved["role"])?.ToString()).IsEqualTo("system");
        await Assert.That((resolved["content"])?.ToString()).IsEqualTo("You are a compiler");
    }

    // Rule 4e — list-of-dicts with nested vars (the BuildGoalCore pattern). Verifies that
    // SubstitutePrimitive recurses into nested dicts on the AsCanonical path, matching the
    // typed AsT path. Without this fix the Content field stayed literal "%buildGoalPrompt%".
    [Test]
    public async Task AsT_PlainDataTarget_ListOfDictsWithNestedVars_DeepResolves()
    {
        var context = _app.User.Context;
        context.Variable.Set("prompt", "You are a compiler");
        context.Variable.Set("user", "build this goal");
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["Role"] = "system", ["Content"] = "%prompt%" },
            new Dictionary<string, object?> { ["Role"] = "user",   ["Content"] = "%user%" }
        };
        var paramData = TemplateStamp.Container("messages", raw, context);

        var canonical = await paramData.AsCanonical();

        // Read the way a real consumer does: enumerate the list, resolve each row, read
        // its field through the door — not a whole-list Lower into raw CLR dictionaries.
        var rows = new List<global::app.type.dict.@this>();
        foreach (var r in (global::app.type.list.@this)(await canonical.Value()))
            rows.Add((global::app.type.dict.@this)(await r.Value()));
        await Assert.That((await rows[0].Get("Content")!.Value()).ToString()).IsEqualTo("You are a compiler");
        await Assert.That((await rows[1].Get("Content")!.Value()).ToString()).IsEqualTo("build this goal");
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
        var paramData = new Data("items", raw, context: context);

        var canonical = await paramData.AsCanonical();

        var resolved = global::app.type.item.@this.Lower<List<object?>>(await canonical.Value())!;
        await Assert.That(resolved.Count).IsEqualTo(3);
        await Assert.That((resolved[0])?.ToString()).IsEqualTo("a");
        await Assert.That((resolved[1])?.ToString()).IsEqualTo("b");
        await Assert.That((resolved[2])?.ToString()).IsEqualTo("c");
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
        context.Variable.Set(new global::app.data.DynamicData("!error", () => "boom"));
        var raw = new Dictionary<string, object?> { ["message"] = "%!error%" };
        var paramData = TemplateStamp.Container("trace.buildError", raw, context);

        var canonical = await paramData.AsCanonical();

        var resolved = global::app.type.item.@this.Lower<Dictionary<string, object?>>(await canonical.Value())!;
        await Assert.That((resolved["message"])?.ToString()).IsEqualTo("boom");
    }
}
