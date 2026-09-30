using app.actor.context;
using app;
using app.type.item.variable;

namespace PLang.Tests.App.actions.variable;

public class SetTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").Testing();
    }

    [Test]
    public async Task Set_SetsVariable()
    {
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%testVar%", "variable"), ("value", "testValue"));
        var result = await action.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("testVar"))).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_BangPath_WritesSetting_NotVariable()
    {
        // `set %!http.request.setting.timeoutInSec% = 5` lands on context.Setting (where the generator seam
        // reads it) — the write side of the setting front door — not on the variable store.
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%!http.request.setting.timeoutInSec%", "variable"), ("value", 5));
        var result = await action.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Setting.Get(_app.Module("http")["request"]!, "timeoutInSec")).Success).IsTrue();
        await Assert.That((await context.Variable.GetValue("!http.request.setting.timeoutInSec"))).IsNull();
    }

    // Build.goal's `set default %!build.setting.cache% = true` leaves --build={"cache":false} standing: a
    // setting's value counts as one already there.
    [Test]
    public async Task SetDefault_OnASetting_KeepsItsValue()
    {
        var context = _app.actor.list.User.Context;
        await _app.actor.list.System.Setting.Set("build.setting.cache", _app.actor.list.System.Context.Ok(false));

        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%!build.setting.cache%", "variable"), ("value", true), ("asDefault", true));
        var result = await action.Start(context);

        await result.IsSuccess();
        var read = await new global::app.type.item.variable.@this("!build.setting.cache").Start(context);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("false");
    }

    [Test]
    public async Task Set_WithType_SetsTypeInfo()
    {
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%count%", "variable"), ("value", 42), ("type", new global::app.type.@this("number", "int")));
        var result = await action.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("count"))!.Type!.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    // A value converted to another type is born: `set %p% as path = "a.txt"` fires the path type's create.
    // The same type re-kinded is the same value, not a birth.
    [Test]
    public async Task Set_AsAnotherType_IsABirth_SameTypeIsNot()
    {
        var context = _app.actor.list.User.Context;
        var births = new List<string>();
        foreach (var name in new[] { "path", "number" })
            _app.type.list[name].Own().Bind("create", global::app.@event.When.after,
                (_, data, c) => { births.Add(data.Type.Name); return Task.FromResult(data); },
                _app.actor.list.User, global::app.@event.binding.Scope.actor);

        var asPath = await global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%p%", "variable"), ("value", "a.txt"), ("type", "path")).Start(context);
        var asNumber = await global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%n%", "variable"), ("value", 42), ("type", new global::app.type.@this("number", "int"))).Start(context);

        await asPath.IsSuccess();
        await asNumber.IsSuccess();
        await Assert.That(births).IsEquivalentTo(new[] { "path" });
    }

    [Test]
    public async Task Create_SourceAlreadyTheType_IsHeldUnread_NoBirth()
    {
        var context = _app.actor.list.User.Context;
        var births = new List<string>();
        var number = _app.type.list["number"];
        number.Own().Bind("create", global::app.@event.When.after,
            (_, data, c) => { births.Add(data.Type.Name); return Task.FromResult(data); },
            _app.actor.list.User, global::app.@event.binding.Scope.actor);
        var unread = number.Make("42", context);

        var result = await number.Create(unread, context, "n");

        await result.IsSuccess();
        await Assert.That(ReferenceEquals(result.Peek(), unread)).IsTrue();
        await Assert.That(result.RawUntouched).IsTrue();
        await Assert.That(births).IsEmpty();
    }

    [Test]
    public async Task Set_ReturnsOk()
    {
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%testVar%", "variable"), ("value", "testValue"));
        var result = await action.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("testVar"))).IsEqualTo("testValue");
        // F3-1: handler must return the stored value, not an empty Data.Ok().
        // Powers %!data% capture in goal.call → ReturnMapping / GoalCallReturn PLang tests.
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("testValue");
    }

    [Test]
    public async Task Set_WithType_SetsTypeOnStoredVariable()
    {
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%count%", "variable"), ("value", 42), ("type", "number"));
        var result = await action.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("count"))!.Type!.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Set_AsDefault_DoesNotOverwriteExisting()
    {
        var context = _app.actor.list.User.Context;

        // Set initial value
        var setAction = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%x%", "variable"), ("value", "original"));
        await setAction.Start(context);

        // Try to set default — should not overwrite
        var defaultAction = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%x%", "variable"), ("value", "default"), ("asdefault", true));
        var result = await defaultAction.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("x"))).IsEqualTo("original");
        // F3-1: when AsDefault hits an existing var, handler returns the existing Data,
        // not an empty Data.Ok(). Reverting that branch would surface here.
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("original");
    }

    [Test]
    public async Task Set_AsDefault_SetsWhenNotExists()
    {
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set", global::PLang.Tests.Shared.Make.Param(context, "Name", "%y%", "variable"), ("value", "default"), ("asdefault", true));
        var result = await action.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("y"))).IsEqualTo("default");
    }

    [Test]
    public async Task ActionRunAsync_AliasesResultUnderData_DoesNotMutateName()
    {
        // F3-2: Action.RunAsync must alias the handler's result under %!data%
        // WITHOUT mutating result.Name. Old code did `result.Name = "!data";
        // Variables.Put(result);` — that corrupted any producer Data shared between
        // %!data% and its source variable entry.
        //
        // Invariant: after RunAsync, %!data% and the handler's own stored entry
        // must be the SAME reference, and the Data's Name must be whatever the
        // handler set it to — never overwritten to "!data".
        var context = _app.actor.list.User.Context;
        var action = global::PLang.Tests.Shared.Make.Action(context, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(context, "Name", "%myVar%", "variable"), ("value", "hello"));

        var result = await action.Start(context);

        await result.IsSuccess();

        var dataVar = await context.Variable.Get("!data");
        var myVar = await context.Variable.Get("myVar");

        // Aliasing: same Data reachable under both keys.
        await Assert.That(ReferenceEquals(dataVar, result)).IsTrue();
        await Assert.That(ReferenceEquals(myVar, result)).IsTrue();

        // No rename: the `value` parameter Data flows through unchanged — Name
        // stays "value" (its parameter name). If RunAsync mutated to "!data",
        // this fires.
        await Assert.That(result.Name).IsNotEqualTo("!data");
    }

    // --- Validate tests (the bound handler judging its own properties) ---

    private static readonly global::app.type.@this NumberInt = new("number", "int");

    private global::app.module.variable.Set WithValue(object value, global::app.type.@this type)
        => new(_app.actor.list.User.Context)
        {
            Value = new Data("Value", value, _app.actor.list.User.Context.App.type.list[type, _app.actor.list.User.Context], context: _app.actor.list.User.Context)
        };

    [Test]
    public async Task Validate_VariableReference_ReturnsNull()
    {
        var result = await WithValue("%myVar%", NumberInt).Validate();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Validate_ValidTypeMatch_ReturnsNull()
    {
        var result = await WithValue(42, NumberInt).Validate();

        await Assert.That(result).IsNull();
    }
}
