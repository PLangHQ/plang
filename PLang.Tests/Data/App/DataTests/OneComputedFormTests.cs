namespace PLang.Tests.App.DataTests;

// A value computed on each read has one form: a Data found per read, answering the whole Data it stands for.
public class OneComputedFormTests
{
    [Test] public async Task ThereIsOneForm()
        => await Assert.That(typeof(global::app.data.DynamicData).GetConstructors().Length).IsEqualTo(1);

    // What it finds answers its value and its properties alike — a value site as the event does.
    [Test] public async Task AFoundData_AnswersItsValueAndItsProperties()
    {
        await using var app = TestApp.Create("/app");
        var ctx = app.actor.list.User.Context;
        var found = ctx.Ok("hello");
        found.Properties.Set("origin", "test");

        var dynamic = new global::app.data.DynamicData("x", _ => found, ctx);

        await Assert.That((await dynamic.Value())?.ToString()).IsEqualTo("hello");
        await Assert.That(await dynamic.Properties.Get<string>("origin")).IsEqualTo("test");
    }

    // Nothing found is the null value and an empty bag.
    [Test] public async Task NothingFound_IsTheNullValue()
    {
        await using var app = TestApp.Create("/app");
        var ctx = app.actor.list.User.Context;

        var dynamic = new global::app.data.DynamicData("x", _ => null, ctx);

        await Assert.That(dynamic.Peek().IsNull).IsTrue();
        await Assert.That(dynamic.Properties.Count).IsEqualTo(0);
    }

    // %Now% is found fresh at each read, built with the asker's context.
    [Test] public async Task Now_IsFoundAtEachRead()
    {
        await using var app = TestApp.Create("/app");
        var ctx = app.actor.list.User.Context;
        var now = ctx.Variable.Peek("Now")!;

        var first = ((global::app.type.item.datetime.@this)now.Peek()).Value;
        await Task.Delay(5);
        var second = ((global::app.type.item.datetime.@this)now.Peek()).Value;

        await Assert.That(second).IsGreaterThan(first);
    }
}
