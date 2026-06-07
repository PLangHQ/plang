namespace PLang.Tests.App.DataTests;

// Phase 2a contract — Name propagation across As<T> resolution.
//
// A parameter Data carries a SLOT name (the handler property name, e.g. "List").
// When the Value is a full %var% match, As<T> resolves to the live variable's
// Data and propagates the variable's NAME — not the slot's. This is what makes
// {%products%}-shaped flows show up in error messages, traces, and __SnapshotParams
// with the variable name the user wrote, rather than the handler's internal slot.
//
// Partial interpolation, literal values, and unset references all KEEP the slot
// name — they're not full-match resolutions, so there's no live variable whose
// name should propagate.

public class NamePropagationTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Full %var% match — Name propagates from the live variable. Param Data is
    // {Name="List", Value="%products%"}. Resolution finds the "products" variable
    // and the result.Name is "products", not "List".
    [Test]
    public async Task Name_FullVarMatch_PropagatesLiveVariableName()
    {
        var context = _app.User.Context;
        context.Variable.Set(new global::app.data.@this("products", global::app.type.list.@this.FromRaw(new List<object?> { "a" }, context)) { Context = context });

        var paramData = new Data("List", "%products%") { Context = context };
        var result = paramData.As<global::app.type.list.@this>();

        await Assert.That(result.Name).IsEqualTo("products");
    }

    // Literal value (no %, just a plain string like "user") — there's no variable
    // to resolve; the parameter Data IS the canonical. result.Name keeps the slot
    // name "Variable". This is Pattern B's contract.
    [Test]
    public async Task Name_LiteralValue_KeepsSlotName()
    {
        var context = _app.User.Context;
        var paramData = new Data("Variable", "user") { Context = context };
        var result = paramData.As<global::app.type.text.@this>();
        await Assert.That(result.Name).IsEqualTo("Variable");
    }

    // Partial interpolation ("hello %name%!") — there's no single variable that
    // owns the resolved string. result.Name keeps the slot name "Greeting".
    [Test]
    public async Task Name_PartialInterpolation_KeepsSlotName()
    {
        var context = _app.User.Context;
        context.Variable.Set(new global::app.data.@this<global::app.type.text.@this>("name", "world") { Context = context });

        var paramData = new Data("Greeting", "hello %name%!") { Context = context };
        var result = paramData.As<global::app.type.text.@this>();
        await Assert.That(result.Name).IsEqualTo("Greeting");
        await Assert.That(result.Value).IsEqualTo("hello world!");
    }

    // Full match against a missing variable. Name still propagates ("missing"),
    // but the result is not initialized.
    [Test]
    public async Task Name_UnsetVariable_PropagatesVarName_NotInitialized()
    {
        var context = _app.User.Context;
        var paramData = new Data("X", "%missing%") { Context = context };
        var result = paramData.As<global::app.type.text.@this>();
        await Assert.That(result.Name).IsEqualTo("missing");
        await Assert.That(result.IsInitialized).IsFalse();
    }

    // Nested-list resolution is a special case of partial interpolation — the
    // outer container resolves piece by piece, no single variable owns the
    // resulting List<object?>. Name keeps the slot ("Items").
    [Test]
    public async Task Name_NestedListResolution_PreservesSlotName()
    {
        var context = _app.User.Context;
        context.Variable.Set(new global::app.data.@this<global::app.type.text.@this>("b", "expanded") { Context = context });

        var paramData = new Data("Items", new List<object?> { "a", "%b%", "c" }) { Context = context };
        var result = paramData.As<global::app.type.list.@this>();
        await Assert.That(result.Name).IsEqualTo("Items");
    }

    // Stored values are values, not expressions: a stored "%b%" is opaque payload,
    // not a chain reference. Name propagates from the IMMEDIATE full-match variable
    // ("a"), never transitively to "b". Matches mainstream language assignment —
    // reading a variable returns the stored bytes verbatim.
    [Test]
    public async Task Name_FullMatch_StoredVarRef_PropagatesImmediateName_NoChain()
    {
        var context = _app.User.Context;
        context.Variable.Set(new global::app.data.@this<global::app.type.number.@this>("b", 42) { Context = context });
        context.Variable.Set(new global::app.data.@this<global::app.type.text.@this>("a", "%b%") { Context = context });

        var paramData = new Data("Slot", "%a%") { Context = context };
        var result = paramData.As<global::app.type.text.@this>();
        await Assert.That(result.Name).IsEqualTo("a");
        await Assert.That(result.Value).IsEqualTo("%b%");
    }
}
