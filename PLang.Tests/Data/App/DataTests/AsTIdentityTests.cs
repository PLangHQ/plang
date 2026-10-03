namespace PLang.Tests.App.DataTests;

// Phase 2b contract — As<T> preserves identity. The architect's anchor: every
// plang variable IS Data; cross-type views are LIVE windows into the same
// variable, sharing Properties by reference. Only Type
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
    public void Setup() => _app = new global::app.@this("/app").Testing();

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Rule 1 — same-type fast path. As<int>() on Data<global::app.type.item.number.@this> returns the source
    // instance. ReferenceEquals is the only check that proves zero allocation
    // and full identity (Properties, Name, Type, everything is
    // trivially shared because it's the same object).
    // Same-type ask is pure pass-through: the typed ask answers the source's own
    // value instance, no conversion and no allocation.
    [Test]
    public async Task AsT_SameType_ReturnsSourceInstance()
    {
        var source = new global::app.data.@this<global::app.type.item.number.@this>("count", 42, context: _app.actor.list.User.Context);
        var result = await source.Value<global::app.type.item.number.@this>();
        await Assert.That(ReferenceEquals(source.Peek(), result)).IsTrue();
    }

    // Trivial corollary of the same-type fast path: Properties is the same ref
    // because the instance is the same. Pinned separately to make the contract
    // explicit — a future "always wrap" optimization would break this test
    // even if it kept value equality.
    [Test]
    public async Task AsT_SameType_PreservesProperties()
    {
        var source = new global::app.data.@this<global::app.type.item.number.@this>("count", 42, context: _app.actor.list.User.Context);
        source.Property.Set("meta", "abc");
        var result = source.As<global::app.type.item.number.@this>(await source.Value<global::app.type.item.number.@this>());
        await Assert.That(ReferenceEquals(source.Property, result.Property)).IsTrue();
        await Assert.That(((await result.Property.Value("meta")))?.ToString()).IsEqualTo("abc");
    }

    // Rule 2 — variance fast path. Data<number>.Value<item>() (to the base item type) produces
    // a new Data<item> instance (Type changes), but .Value is the SAME number.@this reference
    // — cast-only, no copy. (A native list/dict value is a walkable container, so As<T> walks
    // it for nested-var resolution and returns a fresh list — the cast-only ref-share applies
    // to leaf values, which are the ones that never need walking.)
    [Test]
    public async Task AsT_Variance_ScalarToItem_ValueRefShared()
    {
        var inner = (global::app.type.item.number.@this)42;
        var source = new global::app.data.@this<global::app.type.item.number.@this>("n", inner, context: _app.actor.list.User.Context);
        var wrapped = source.As<global::app.type.item.@this>(await source.Value<global::app.type.item.@this>());
        await Assert.That(ReferenceEquals(source, wrapped)).IsFalse();
        await Assert.That(ReferenceEquals((await wrapped.Value()), inner)).IsTrue();
    }

    // Variance fast path aliases Properties from source onto the wrapped Data.
    // ref-equal: Adding to source.Property is visible via wrapped.Property
    // because they ARE the same Properties bag.
    [Test]
    public async Task AsT_Variance_PropertiesAliased()
    {
        var inner = new global::app.type.item.list.@this<global::app.type.item.number.@this>(new[] { _app.Data("", 1), _app.Data("", 2) });
        var source = new global::app.data.@this<global::app.type.item.list.@this<global::app.type.item.number.@this>>("nums", inner, context: _app.actor.list.User.Context);
        var wrapped = source.As<global::app.type.item.list.@this>(await source.Value<global::app.type.item.list.@this>());
        await Assert.That(ReferenceEquals(source.Property, wrapped.Property)).IsTrue();
        source.Property.Set("annot", "via-source");
        await Assert.That(((await wrapped.Property.Value("annot")))?.ToString()).IsEqualTo("via-source");
    }



    // Rule 3 — cross-type with conversion. Data<global::app.type.item.number.@this>(42).Value<global::app.type.item.text.@this>() produces
    // a NEW Data<global::app.type.item.text.@this> with converted .Value ("42"), but Properties
    // alias from source. The .Value is a fresh converted object —
    // ref-DISTINCT from source.Value (42 boxed) — but the metadata bag is shared.
    [Test]
    public async Task AsT_CrossType_ConversionWraps_PropertiesAliased()
    {
        var source = new global::app.data.@this<global::app.type.item.number.@this>("count", 42, context: _app.actor.list.User.Context);
        source.Property.Set("note", "hello");
        var wrapped = source.As<global::app.type.item.text.@this>(await source.Value<global::app.type.item.text.@this>());
        await Assert.That(ReferenceEquals(source, wrapped)).IsFalse();
        await Assert.That((await wrapped.Value())?.ToString()).IsEqualTo("42");
        await Assert.That(ReferenceEquals(source.Property, wrapped.Property)).IsTrue();
        await Assert.That(((await wrapped.Property.Value("note")))?.ToString()).IsEqualTo("hello");
    }

    // Conversion failure path. The typed ask on a value that can't convert to T
    // answers null and lands the decline on the asking binding (source.Success
    // becomes false) — the error rides the binding the caller already holds, no
    // separate sentinel is minted.
    [Test]
    public async Task AsT_CrossType_ConversionFailure_DeclinesOnSource()
    {
        var source = new global::app.data.@this<global::app.type.item.text.@this>("messy", "not-a-number", context: _app.actor.list.User.Context);
        var result = await source.Value<global::app.type.item.number.@this>();
        await Assert.That(result).IsNull();
        await source.IsFailure();
    }

    // A literal (no %) is its own Data: following it answers itself.
    [Test]
    public async Task AsT_PlainDataTarget_LiteralParameter_ReturnsParameterDataAsIs()
    {
        var paramData = new Data("Slot", "literal value", context: _app.actor.list.User.Context);
        var canonical = await paramData.Follow(_app.actor.list.User.Context);
        await Assert.That(ReferenceEquals(paramData, canonical)).IsTrue();
    }

    // A full-match %var% follows to the LIVE variable Data: mutations through it are visible
    // through Variables.Get.
    [Test]
    public async Task AsT_PlainDataTarget_VarReference_ReturnsLiveVariableData()
    {
        var context = _app.actor.list.User.Context;
        var live = new global::app.data.@this("products", global::PLang.Tests.Shared.Make.List(new List<object?> { "a", "b" }, context), context: context);
        context.Variable.Set(live);

        var paramData = PLang.Tests.Shared.Make.Built(context, "Slot", "%products%");
        var canonical = await paramData.Follow(context);

        await Assert.That(ReferenceEquals(canonical, live)).IsTrue();
        // Mutation propagates: appending via live's value is visible through Variables.Get.
        ((global::app.type.item.list.@this)(await canonical.Value())!).Add(_app.Data("", "c"));
        var stored = (global::app.type.item.list.@this)(await (await context.Variable.Get("products")).Value())!;
        await Assert.That(stored.Count).IsEqualTo(3);
    }

    // A template list's value door resolves the %var% references its elements hold.
    [Test]
    public async Task AsT_PlainDataTarget_ListWithNestedVars_Resolves()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("greeting", "hello");
        var raw = new List<object?> { "%greeting%", "literal" };
        var paramData = TemplateStamp.Container("Slot", raw, context);

        var canonical = paramData;

        var resolved = Lower<List<object?>>(await canonical.Value())!;
        await Assert.That((resolved[0])?.ToString()).IsEqualTo("hello");
        await Assert.That((resolved[1])?.ToString()).IsEqualTo("literal");
    }

    // A template dict's value door resolves the %var% references its values hold.
    [Test]
    public async Task AsT_PlainDataTarget_DictWithNestedVars_Resolves()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("prompt", "You are a compiler");
        var raw = new Dictionary<string, object?> { ["role"] = "system", ["content"] = "%prompt%" };
        var paramData = TemplateStamp.Container("Slot", raw, context);

        var canonical = paramData;

        var resolved = Lower<Dictionary<string, object?>>(await canonical.Value())!;
        await Assert.That((resolved["role"])?.ToString()).IsEqualTo("system");
        await Assert.That((resolved["content"])?.ToString()).IsEqualTo("You are a compiler");
    }

    // A template list of dicts (an llm message list) resolves the %var% references inside each dict.
    [Test]
    public async Task AsT_PlainDataTarget_ListOfDictsWithNestedVars_DeepResolves()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("prompt", "You are a compiler");
        context.Variable.Set("user", "build this goal");
        var raw = new List<object?>
        {
            new Dictionary<string, object?> { ["Role"] = "system", ["Content"] = "%prompt%" },
            new Dictionary<string, object?> { ["Role"] = "user",   ["Content"] = "%user%" }
        };
        var paramData = TemplateStamp.Container("messages", raw, context);

        var canonical = paramData;

        // Read the way a real consumer does: enumerate the list, resolve each row, read
        // its field through the door — not a whole-list Lower into raw CLR dictionaries.
        var rows = new List<global::app.type.item.dict.@this>();
        foreach (var r in ((global::app.type.item.list.@this)(await canonical.Value())).Items(_app.actor.list.User.Context))
            rows.Add((global::app.type.item.dict.@this)(await r.Value()));
        await Assert.That((await rows[0].Get("Content", _app.actor.list.User.Context)!.Value()).ToString()).IsEqualTo("You are a compiler");
        await Assert.That((await rows[1].Get("Content", _app.actor.list.User.Context)!.Value()).ToString()).IsEqualTo("build this goal");
    }

    // A literal list (no %vars%) reads back its values unchanged.
    [Test]
    public async Task AsT_PlainDataTarget_LiteralList_NoNestedVars_PreservesValues()
    {
        var context = _app.actor.list.User.Context;
        var raw = new List<object?> { "a", "b", "c" };
        var paramData = new Data("items", raw, context: context);

        var canonical = paramData;

        var resolved = Lower<List<object?>>(await canonical.Value())!;
        await Assert.That(resolved.Count).IsEqualTo(3);
        await Assert.That((resolved[0])?.ToString()).IsEqualTo("a");
        await Assert.That((resolved[1])?.ToString()).IsEqualTo("b");
        await Assert.That((resolved[2])?.ToString()).IsEqualTo("c");
    }

    // A %!var% inside a template container resolves like any %var%:
    // `set %trace.buildError% = {"message": "%!error.Message%"}` records the error, not the literal.
    [Test]
    public async Task AsT_PlainDataTarget_DictWithInfraVar_Resolves()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set(new global::app.data.DynamicData("!error", asker => asker.Ok("boom"), context));
        var raw = new Dictionary<string, object?> { ["message"] = "%!error%" };
        var paramData = TemplateStamp.Container("trace.buildError", raw, context);

        var canonical = paramData;

        var resolved = Lower<Dictionary<string, object?>>(await canonical.Value())!;
        await Assert.That((resolved["message"])?.ToString()).IsEqualTo("boom");
    }
}
