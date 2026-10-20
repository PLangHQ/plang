namespace PLang.Tests.App.Types;

// A dict's key, read with `!`: alone it is the first key, `.list` every key in the order they were set, and it
// holds its dict — a key set after it was read is in it.
public class DictKeyTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Context => app.actor.list.User.Context;

    private async Task<global::app.data.@this> Read(string variable)
        => await new global::app.type.item.variable.parser.@this(variable, Context.App.type.list).Whole!.Start(Context);

    private global::app.type.item.dict.@this Person()
    {
        var person = new global::app.type.item.dict.@this(Context.App.type.list["dict"]);
        person.Set(new global::app.data.@this("name", "Ada", context: Context));
        person.Set(new global::app.data.@this("age", 36L, context: Context));
        Context.Variable.Set(new global::app.data.@this("person", person, context: Context));
        return person;
    }

    [Test] public async Task Key_ReadAlone_IsTheFirstKey()
    {
        Person();
        var key = await Read("%person!key%");
        await Assert.That((await key.Value())?.ToString()).IsEqualTo("name");
    }

    // a dict still its unread source (a .pr's literal, a json read) opens when its key is read, as a dot opens it
    [Test] public async Task Key_OfAnUnreadDict_OpensIt_AndIsTheFirstKey()
    {
        var slice = Context.App.type.list["dict"].Make("{\"name\":\"Ada\",\"age\":36}",
            (global::app.type.item.wire.kind.plang.@this)Context.App.type.list["wire"].kind["plang"]!);
        Context.Variable.Set(new global::app.data.@this("person", slice, context: Context));

        var key = await Read("%person!key%");
        await Assert.That((await key.Value())?.ToString()).IsEqualTo("name");
    }

    [Test] public async Task KeyList_IsEveryKey_InOrder()
    {
        Person();
        var keys = await (await Read("%person!key.list%")).Value() as global::app.type.item.list.@this;
        await Assert.That(keys!.Items(Context).Select(k => k.Peek().ToString()).ToList()).IsEquivalentTo(new[] { "name", "age" });
    }

    [Test] public async Task Key_HoldsItsDict_AKeySetLaterIsInIt()
    {
        var person = Person();
        var key = person.Key(Context);
        person.Set(new global::app.data.@this("city", "London", context: Context));
        await Assert.That(key.list(Context).Items(Context).Select(k => k.Peek().ToString()).ToList())
            .IsEquivalentTo(new[] { "name", "age", "city" });
    }

    [Test] public async Task AnEmptyDictsKey_IsNull()
    {
        var empty = new global::app.type.item.dict.@this(Context.App.type.list["dict"]);
        var value = await empty.Key(Context).Value(new global::app.data.@this("", empty, context: Context));
        await Assert.That(value.IsNull).IsTrue();
    }
}
