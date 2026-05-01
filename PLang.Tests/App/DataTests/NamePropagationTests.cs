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
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::App.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // Full %var% match — Name propagates from the live variable. Param Data is
    // {Name="List", Value="%products%"}. Resolution finds the "products" variable
    // and the result.Name is "products", not "List".
    [Test]
    public async Task Name_FullVarMatch_PropagatesLiveVariableName()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new global::App.Data.@this<List<object?>>("products", new List<object?> { "a" }) { Context = ctx });

        var paramData = new Data("List", "%products%") { Context = ctx };
        var result = paramData.As<System.Collections.IEnumerable>();

        await Assert.That(result.Name).IsEqualTo("products");
    }

    // Literal value (no %, just a plain string like "user") — there's no variable
    // to resolve; the parameter Data IS the canonical. result.Name keeps the slot
    // name "Variable". This is Pattern B's contract.
    [Test]
    public async Task Name_LiteralValue_KeepsSlotName()
    {
        var ctx = _app.User.Context;
        var paramData = new Data("Variable", "user") { Context = ctx };
        var result = paramData.As<string>();
        await Assert.That(result.Name).IsEqualTo("Variable");
    }

    // Partial interpolation ("hello %name%!") — there's no single variable that
    // owns the resolved string. result.Name keeps the slot name "Greeting".
    [Test]
    public async Task Name_PartialInterpolation_KeepsSlotName()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new global::App.Data.@this<string>("name", "world") { Context = ctx });

        var paramData = new Data("Greeting", "hello %name%!") { Context = ctx };
        var result = paramData.As<string>();
        await Assert.That(result.Name).IsEqualTo("Greeting");
        await Assert.That(result.Value).IsEqualTo("hello world!");
    }

    // Full match against a missing variable. Name still propagates ("missing"),
    // but the result is not initialized.
    [Test]
    public async Task Name_UnsetVariable_PropagatesVarName_NotInitialized()
    {
        var ctx = _app.User.Context;
        var paramData = new Data("X", "%missing%") { Context = ctx };
        var result = paramData.As<string>();
        await Assert.That(result.Name).IsEqualTo("missing");
        await Assert.That(result.IsInitialized).IsFalse();
    }

    // Nested-list resolution is a special case of partial interpolation — the
    // outer container resolves piece by piece, no single variable owns the
    // resulting List<object?>. Name keeps the slot ("Items").
    [Test]
    public async Task Name_NestedListResolution_PreservesSlotName()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new global::App.Data.@this<string>("b", "expanded") { Context = ctx });

        var paramData = new Data("Items", new List<object?> { "a", "%b%", "c" }) { Context = ctx };
        var result = paramData.As<System.Collections.IEnumerable>();
        await Assert.That(result.Name).IsEqualTo("Items");
    }

    // Chained full-match — %slot% → "%a%" → "%b%" → 42. The live variable
    // owning the final value is "b"; result.Name == "b".
    [Test]
    public async Task Name_ChainedFullMatch_PropagatesFinalName()
    {
        var ctx = _app.User.Context;
        ctx.Variables.Set(new global::App.Data.@this<int>("b", 42) { Context = ctx });
        ctx.Variables.Set(new global::App.Data.@this<string>("a", "%b%") { Context = ctx });

        var paramData = new Data("Slot", "%a%") { Context = ctx };
        var result = paramData.As<int>();
        await Assert.That(result.Name).IsEqualTo("b");
        await Assert.That(result.Value).IsEqualTo(42);
    }
}
