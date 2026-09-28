namespace PLang.Tests.App.Types;

// %!app.type.list% is every type a program can use — the built-ins and the concepts alike; a type is found in
// it by its name field.
public class AppTypeListTests
{
    private static async Task<global::app.data.@this> Read(global::app.@this app, string path)
        => await new global::app.type.item.variable.parser.@this(path).Variable.Single().Start(app.actor.list.User.Context);

    private static async Task<bool> Holds(global::app.@this app, string name)
    {
        var ctx = app.actor.list.User.Context;
        var list = await (await Read(app, "%!app.type.list%")).Value() as global::app.type.item.list.@this;
        var any = await list!.Any("name", new global::app.data.Operator("=="), ctx.Ok(name), ctx);
        return any.ToBoolean();
    }

    [Test] public async Task TheTypeList_HoldsTheBuiltInsAndTheConcepts()
    {
        await using var app = TestApp.Create("/app");
        await Assert.That(await Holds(app, "text")).IsTrue();
        await Assert.That(await Holds(app, "goal")).IsTrue();
        await Assert.That(await Holds(app, "no-such-type")).IsFalse();
    }
}
