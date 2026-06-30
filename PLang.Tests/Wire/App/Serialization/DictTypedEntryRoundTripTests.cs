namespace PLang.Tests.App.Serialization;

// A dict value whose entries are typed values ({type, value}, no @schema) must
// round-trip through the Store-view wire with each entry's TYPE preserved — the
// shape an llm.query structured result (cached to settings) relies on. The store
// self-describes every entry; the read must born each {type, value} entry back to
// its type, not leave it a raw {type, value} dict.
public class DictTypedEntryRoundTripTests
{
    private static global::app.@this NewApp()
        => global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-dictentry-" + System.Guid.NewGuid().ToString("N")[..8]));

    // Acceptance test for the context-never-null work: a dict of nested typed entries
    // ({type,value}, no @schema) round-trips through the Store wire with each entry's type
    // preserved. With context never null + the hash-in-serialization-view fix, the read borns
    // each nested entry back to its real type. The value is read lazily, so nested types
    // materialize through the async Value door — Peek returns the deferred source.
    [Test]
    public async Task DictOfTypedEntries_StoreRoundTrip_PreservesNestedTypes()
    {
        await using var app = NewApp();
        var context = app.User.Context;

        // value = dict { description: text, steps: list[ dict{index:number} ] }
        var steps = new global::app.type.list.@this { Context = context }
            .Add(new global::app.data.@this("", new global::app.type.dict.@this { Context = context }.Set("index", 0L), context: context));
        var value = new global::app.type.dict.@this { Context = context }
            .Set("description", "a plan")
            .Set("steps", steps);
        // Born WITH context — never constructed then stamped.
        var result = context.Ok(value);

        // The store binds the serializer with its context (application/plang) — the read
        // routes through the one context-ful Typed-reader path, borning each nested
        // {type,value} entry back to its real type instead of a raw dict.
        var serializer = new global::app.channel.serializer.plang.@this(context);
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeAsync(ms, result, global::app.View.Store);
        ms.Position = 0;
        var back = await serializer.DeserializeAsync<global::app.type.item.@this>(ms, global::app.View.Store);

        await Assert.That(back.Success).IsTrue();
        // Materialize through the async Value door — Peek returns the deferred source.
        var dict = (await back.Value()) as global::app.type.dict.@this;
        await Assert.That(dict).IsNotNull();
        await Assert.That(dict!.Get("steps")!.Type?.Name).IsEqualTo("list");
        await Assert.That(dict.Get("description")!.Type?.Name).IsEqualTo("text");
    }
}
