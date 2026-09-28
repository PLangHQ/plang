using @this = global::app.type.item.variable.@this;

namespace PLang.Tests.App.VariablesTests;

// A variable is its text and its code. Variable.Resolve is invoked by the source generator's
// Data<T> emit through the Data.Value<T> raw-name dispatch (variable is an IName); "%x%" and the
// bare "x" a name slot may carry are the same variable.

public class VariableResolveTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/test");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task Resolve_PercentWrapped_IsTextAndRootCode()
    {
        var v = @this.Resolve("%x%", _app.actor.list.User.Context);

        await Assert.That(v.Text).IsEqualTo("%x%");
        await Assert.That(v.Name).IsEqualTo("x");
        await Assert.That(v.Code.Count).IsEqualTo(1);
        await Assert.That(v.Code.Root.Name).IsEqualTo("x");
    }

    [Test]
    public async Task Resolve_BareName_IsTheSameVariable()
    {
        var v = @this.Resolve("x", _app.actor.list.User.Context);

        await Assert.That(v.Text).IsEqualTo("%x%");
        await Assert.That(v.Name).IsEqualTo("x");
    }

    [Test]
    public async Task Resolve_EmptyString_IsNotAVariable()
    {
        await Assert.That(() => @this.Resolve("", _app.actor.list.User.Context))
            .Throws<global::app.error.AppException>();
    }

    [Test]
    public async Task Convert_NotAVariable_DeclinesWithTheParsersReason()
    {
        var born = @this.Convert("%x!!cost%", null, _app.actor.list.User.Context);

        await born.IsFailure();
        await Assert.That(born.Error!.Key).IsEqualTo("InvalidVariable");
    }

    // A born Data<variable> slot — its value is already a variable. The typed ask passes it
    // through unchanged: a name is never rendered against the store, so an unset "x" still
    // yields its variable.
    [Test]
    public async Task SlotData_AsVariable_NameIsX()
    {
        var slot = new global::app.data.@this<@this>("Name", @this.Resolve("%x%", _app.actor.list.User.Context), context: _app.actor.list.User.Context);

        var resolved = await slot.Value<@this>();

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Name).IsEqualTo("x");
    }

    // A variable carries identity, not value: even when "x" holds 5, the ask returns the variable.
    [Test]
    public async Task SlotData_AsVariable_IgnoresExistingValue()
    {
        await _app.actor.list.User.Context.Variable.Set("x", 5);
        var slot = new global::app.data.@this<@this>("Name", @this.Resolve("%x%", _app.actor.list.User.Context), context: _app.actor.list.User.Context);

        var resolved = await slot.Value<@this>();

        await Assert.That(resolved!.Text).IsEqualTo("%x%");
    }

    [Test]
    public async Task ImplicitConversion_ToString_ReturnsName()
    {
        @this v = new @this("x");

        string s = v;

        await Assert.That(s).IsEqualTo("x");
    }

    [Test]
    public async Task ToString_ReturnsName_ForInterpolationFriendliness()
    {
        var v = new @this("listName");

        var formatted = $"Variable '{v}' was missing";

        await Assert.That(formatted).IsEqualTo("Variable 'listName' was missing");
    }
}
