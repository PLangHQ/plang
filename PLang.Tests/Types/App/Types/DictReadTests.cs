namespace PLang.Tests.App.Types;

// A dict's entry is made at its read and nothing is written back: a stored entry is read without being changed, and
// a Data handed in under another name is held under its key without renaming the one who handed it. Lowered to C#, a
// dict is a C# dictionary of its own, keeping its case-insensitive keys.
public class DictReadTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Context => app.actor.list.User.Context;

    private global::app.type.item.dict.@this Dict() => new(Context.App.type.list["dict"]);

    [Test] public async Task ADictLoweredToCSharp_IsADictOfItsOwn()
    {
        var dict = Dict();
        dict.Set("name", "Ada");

        var lowered = dict.Clr<Dictionary<string, object?>>()!;
        lowered["age"] = 36L;

        await Assert.That(dict.CountRaw).IsEqualTo(1);
        await Assert.That(lowered.ContainsKey("NAME")).IsTrue();
    }

    [Test] public async Task ADictLoweredToObject_IsADictOfItsOwn_WhateverItHolds()
    {
        var dict = Dict();
        dict.Set("name", "Ada");
        dict.Set(new global::app.data.@this("age", 36L, context: Context));

        var lowered = dict.Clr<object>() as Dictionary<string, object?>;

        await Assert.That(lowered).IsNotNull();
        await Assert.That(lowered!.ContainsKey("AGE")).IsTrue();
        await Assert.That(lowered["name"]).IsEqualTo("Ada");
    }

    [Test] public async Task AStoredEntry_IsReadWithoutChangingIt()
    {
        var dict = Dict();
        var given = new global::app.data.@this("x", "v", context: Context);
        dict.Set("a", given);

        var read = dict.Get("a", Context)!;

        await Assert.That(read.Name).IsEqualTo("a");
        await Assert.That(given.Name).IsEqualTo("x");
    }
}
