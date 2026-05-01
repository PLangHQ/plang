using global::App.Variables;
using Variable = global::App.Variables.Variable;

namespace PLang.Tests.App.VariablesTests;

// Contract tests for App.Variables.Variable — the typed variable-name carrier introduced
// in architect/v5 to replace [VariableName] string. Variable.Resolve is invoked by the
// source generator's Data<T> emit through the Data.As<T> raw-name dispatch (Variable
// implements IRawNameResolvable). Symmetry contract: both "%x%" and "x" produce Name == "x".

public class VariableResolveTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::App.@this("/test");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task Resolve_PercentWrapped_StripsAndFlags()
    {
        var v = Variable.Resolve("%x%", _app.Context);

        await Assert.That(v.Name).IsEqualTo("x");
        await Assert.That(v.RawValue).IsEqualTo("%x%");
        await Assert.That(v.WasPercentWrapped).IsTrue();
    }

    [Test]
    public async Task Resolve_BareName_KeepsNameSurfacesFlag()
    {
        var v = Variable.Resolve("x", _app.Context);

        await Assert.That(v.Name).IsEqualTo("x");
        await Assert.That(v.RawValue).IsEqualTo("x");
        await Assert.That(v.WasPercentWrapped).IsFalse();
    }

    [Test]
    public async Task Resolve_EmptyString_ProducesEmptyVariable()
    {
        var v = Variable.Resolve("", _app.Context);

        await Assert.That(v.Name).IsEqualTo("");
        await Assert.That(v.RawValue).IsEqualTo("");
        await Assert.That(v.WasPercentWrapped).IsFalse();
    }

    // Slot Data carrying "%x%" must resolve through As<Variable>(ctx) to a Variable
    // whose Name is "x". This is the load-bearing contract for the migration:
    // every former [VariableName] slot will be Data<Variable> after Phase 2. The
    // raw-name carve-out in Data.AsT_Impl bypasses %var% substitution for
    // IRawNameResolvable Ts so this works even when "x" is uninitialized (the
    // common case for variable.set creating a new variable).
    [Test]
    public async Task SlotData_PercentWrapped_AsVariable_NameIsX()
    {
        var slot = new Data("Name", "%x%") { Context = _app.Context };

        var resolved = slot.As<Variable>(_app.Context);

        await Assert.That(resolved.Success).IsTrue();
        await Assert.That(resolved.Value).IsNotNull();
        await Assert.That(resolved.Value!.Name).IsEqualTo("x");
        await Assert.That(resolved.Value!.WasPercentWrapped).IsTrue();
    }

    // Pinning the symmetry contract from the architect's plan: "x" pre-existing
    // in Variables must NOT short-circuit and produce the value of x. The
    // Variable carries the *identity*, not the value. This is the case the
    // raw-name carve-out is designed for; without it, TryFullVarMatch would
    // return x's live value (5) and downstream conversion would either succeed
    // wrongly or fail with a conversion error.
    [Test]
    public async Task SlotData_PercentWrapped_AsVariable_IgnoresExistingValue()
    {
        _app.Context.Variables.Set("x", 5);
        var slot = new Data("Name", "%x%") { Context = _app.Context };

        var resolved = slot.As<Variable>(_app.Context);

        await Assert.That(resolved.Success).IsTrue();
        await Assert.That(resolved.Value!.Name).IsEqualTo("x");
        await Assert.That(resolved.Value!.WasPercentWrapped).IsTrue();
    }

    // Slot Data carrying bare "x" — the LLM-emission case that broke the v1 branch.
    // Variable.Resolve handles it symmetrically.
    [Test]
    public async Task SlotData_BareName_AsVariable_NameIsX()
    {
        var slot = new Data("Name", "x") { Context = _app.Context };

        var resolved = slot.As<Variable>(_app.Context);

        await Assert.That(resolved.Success).IsTrue();
        await Assert.That(resolved.Value).IsNotNull();
        await Assert.That(resolved.Value!.Name).IsEqualTo("x");
    }

    // Implicit Variable→string conversion fires at any string-expecting boundary
    // (e.g. Variables.Get(name.Value), method-call sites in handlers).
    [Test]
    public async Task ImplicitConversion_ToString_ReturnsName()
    {
        Variable v = new Variable("x", "%x%", true);

        string s = v;

        await Assert.That(s).IsEqualTo("x");
    }

    // String interpolation calls ToString — overridden to return Name so error
    // messages and logs read naturally.
    [Test]
    public async Task ToString_ReturnsName_ForInterpolationFriendliness()
    {
        var v = new Variable("listName", "%listName%", true);

        var formatted = $"Variable '{v}' was missing";

        await Assert.That(formatted).IsEqualTo("Variable 'listName' was missing");
    }
}
