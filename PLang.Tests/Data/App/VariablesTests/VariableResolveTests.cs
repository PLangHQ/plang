using app.variable;
using @this = global::app.variable.@this;

namespace PLang.Tests.App.VariablesTests;

// Contract tests for App.Variables.Variable — the typed variable-name carrier introduced
// in architect/v5 to replace [VariableName] string. Variable.Resolve is invoked by the
// source generator's Data<T> emit through the Data.Value<T> raw-name dispatch (Variable
// implements IRawNameResolvable). Symmetry contract: both "%x%" and "x" produce Name == "x".

public class VariableResolveTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/test");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task Resolve_PercentWrapped_StripsAndFlags()
    {
        var v = @this.Resolve("%x%", _app.User.Context);

        await Assert.That(v.Name).IsEqualTo("x");
        await Assert.That(v.RawValue).IsEqualTo("%x%");
        await Assert.That(v.WasPercentWrapped).IsTrue();
    }

    [Test]
    public async Task Resolve_BareName_KeepsNameSurfacesFlag()
    {
        var v = @this.Resolve("x", _app.User.Context);

        await Assert.That(v.Name).IsEqualTo("x");
        await Assert.That(v.RawValue).IsEqualTo("x");
        await Assert.That(v.WasPercentWrapped).IsFalse();
    }

    [Test]
    public async Task Resolve_EmptyString_ProducesEmptyVariable()
    {
        var v = @this.Resolve("", _app.User.Context);

        await Assert.That(v.Name).IsEqualTo("");
        await Assert.That(v.RawValue).IsEqualTo("");
        await Assert.That(v.WasPercentWrapped).IsFalse();
    }

    // A born Data<Variable> slot — its value is already a Variable (the wire
    // boundary births it via type.Judge → Variable.Resolve). The typed ask
    // passes the Variable through unchanged: a name is never rendered against
    // the store, so an uninitialized "x" still yields its Variable.
    [Test]
    public async Task SlotData_PercentWrapped_AsVariable_NameIsX()
    {
        var slot = new global::app.data.@this<@this>("Name", @this.Resolve("%x%", _app.User.Context)) { Context = _app.User.Context };

        var resolved = await slot.Value<@this>();

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Name).IsEqualTo("x");
        await Assert.That(resolved.WasPercentWrapped).IsTrue();
    }

    // A variable carries identity, not value: even when "x" holds 5 in the
    // store, the ask returns the Variable (Name "x"), never x's value.
    [Test]
    public async Task SlotData_PercentWrapped_AsVariable_IgnoresExistingValue()
    {
        _app.User.Context.Variable.Set("x", 5);
        var slot = new global::app.data.@this<@this>("Name", @this.Resolve("%x%", _app.User.Context)) { Context = _app.User.Context };

        var resolved = await slot.Value<@this>();

        await Assert.That(resolved!.Name).IsEqualTo("x");
        await Assert.That(resolved.WasPercentWrapped).IsTrue();
    }

    // Bare "x" (no percent) births the same Variable — symmetry with "%x%".
    [Test]
    public async Task SlotData_BareName_AsVariable_NameIsX()
    {
        var slot = new global::app.data.@this<@this>("Name", @this.Resolve("x", _app.User.Context)) { Context = _app.User.Context };

        var resolved = await slot.Value<@this>();

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Name).IsEqualTo("x");
    }

    // Implicit Variable→string conversion fires at any string-expecting boundary
    // (e.g. Variables.Get(name.Value), method-call sites in handlers).
    [Test]
    public async Task ImplicitConversion_ToString_ReturnsName()
    {
        @this v = new global::app.variable.@this("x", "%x%", true);

        string s = v;

        await Assert.That(s).IsEqualTo("x");
    }

    // String interpolation calls ToString — overridden to return Name so error
    // messages and logs read naturally.
    [Test]
    public async Task ToString_ReturnsName_ForInterpolationFriendliness()
    {
        var v = new global::app.variable.@this("listName", "%listName%", true);

        var formatted = $"Variable '{v}' was missing";

        await Assert.That(formatted).IsEqualTo("Variable 'listName' was missing");
    }
}
