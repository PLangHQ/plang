using app.actor.context;
using app;
using app.type.item.variable;

namespace PLang.Tests.App.Context;

public class ContextVariableTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/test").Testing();
    }

    [Test]
    public async Task ContextVar_Engine_ReturnsEngineInstance()
    {
        var vars = _app.actor.list.User.Context.Variable;
        var value = await vars.GetValue("!app");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsEqualTo(_app);
    }

    [Test]
    public async Task ContextVar_Context_ReturnsPLangContext()
    {
        var vars = _app.actor.list.User.Context.Variable;
        var value = await vars.GetValue("!context");

        await Assert.That(value).IsNotNull();
        await Assert.That(value).IsTypeOf<global::app.actor.context.@this>();
    }


    // %!callStack% is the app's member answering as its asker — not a registration in memory.
    [Test]
    public async Task ContextVar_CallStack_ReturnsCallStack()
    {
        var context = _app.actor.list.User.Context;
        var read = await new global::app.type.item.variable.parser.@this("%!callStack%").Variable.Single().Start(context);

        await Assert.That(read.Peek()).IsSameReferenceAs(context.CallStack);
    }

    // the goal, step, error, test and channels in play are shortcuts, not memory: SystemShortcutTests
    [Test]
    [Arguments("!goal")]
    [Arguments("!step")]
    [Arguments("!error")]
    [Arguments("!test")]
    [Arguments("!channels")]
    [Arguments("!variables")]
    public async Task ThePlaceInPlay_IsNoContextVariable(string name)
    {
        await Assert.That(_app.actor.list.User.Context.Variable.Contains(name)).IsFalse();
    }

    [Test]
    public async Task ContextVars_ExcludedFromGetNames()
    {
        var vars = _app.actor.list.User.Context.Variable;
        vars.Set("regularVar", "hello");

        var names = vars.GetNames().ToList();

        await Assert.That(names).Contains("regularVar");
        await Assert.That(names).DoesNotContain("!app");
        await Assert.That(names).DoesNotContain("!context");
        await Assert.That(names).DoesNotContain("!goal");
    }

    [Test]
    public async Task ContextVars_ExcludedFromGetAll()
    {
        var vars = _app.actor.list.User.Context.Variable;
        vars.Set("regularVar", "hello");

        var all = vars.GetAll().ToList();
        var names = all.Select(kvp => kvp.Key).ToList();

        await Assert.That(names).Contains("regularVar");
        await Assert.That(names).DoesNotContain("!app");
        await Assert.That(names).DoesNotContain("!goal");
    }

    [Test]
    public async Task ContextVars_SurviveClear()
    {
        var vars = _app.actor.list.User.Context.Variable;
        vars.Set("regularVar", "hello");

        vars.Clear();

        // Regular var is gone
        await Assert.That((await vars.GetValue("regularVar"))).IsNull();

        // Context vars survive
        await Assert.That((await vars.GetValue("!app"))).IsNotNull();
        await Assert.That((await vars.GetValue("!context"))).IsNotNull();
    }

    [Test]
    public async Task ContextVars_NotCloned()
    {
        var vars = _app.actor.list.User.Context.Variable;
        vars.Set("regularVar", "hello");

        var clone = vars.Clone();

        // Regular var is cloned
        await Assert.That((await clone.GetValue("regularVar"))).IsEqualTo("hello");

        // Context vars are NOT cloned (they'd break as plain Data objects)
        await Assert.That(clone.Contains("!app")).IsFalse();
    }

    [Test]
    public async Task DynamicData_ValueResolvesViaBaseReference()
    {
        // Proves the virtual/override fix: accessing .Value through a Data reference
        // correctly calls DynamicData.Value (not base Data.Value which returns null)
        var vars = _app.actor.list.User.Context.Variable;

        // Now is a DynamicData registered by Variables constructor
        var nowValue = await vars.GetValue("Now");
        await Assert.That(nowValue).IsNotNull();
        await Assert.That(nowValue).IsTypeOf<DateTimeOffset>();

        // !context is a DynamicData registered by RegisterContextVariables
        await Assert.That(await vars.GetValue("!context")).IsSameReferenceAs(_app.actor.list.User.Context);
    }

    [Test]
    public async Task ContextVar_AppProperty_AccessibleViaDotNotation()
    {
        var vars = _app.actor.list.User.Context.Variable;
        var data = await new global::app.type.item.variable.@this("!app.Name").Start(vars.Context);

        await Assert.That(data).IsNotNull();
        await Assert.That((await data!.Value())?.ToString()).IsEqualTo("test");
    }
}
