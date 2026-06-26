namespace PLang.Tests.App.Serialization;

// A dict value whose entries are typed values ({type, value}, no @schema) must
// round-trip through the Store-view wire with each entry's TYPE preserved — the
// shape an llm.query structured result (cached to settings) relies on. The store
// self-describes every entry; the read must born each {type, value} entry back to
// its type, not leave it a raw {type, value} dict.
public class DictTypedEntryRoundTripTests
{
    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-dictentry-" + System.Guid.NewGuid().ToString("N")[..8]));

    // Acceptance test for the context-never-null work (.bot/remove-goalcall/coder/
    // plan.md): a context-less Store read drops nested typed entries
    // to raw {type,value} dicts. Goes green when the context-less read narrow is retired
    // and every Store read routes through the one context-ful Typed-reader path.
    // Type-routing is verified: with a context-ful serializer the read borns each
    // nested {type,value} entry back to its type (the context-less narrow is gone).
    // Still skipped pending Stage 5: the context-ful Store path signs on write and
    // verifies on read, and the re-read value must canonicalize identically to the
    // signed original (currently DataHashMismatch — verify-on-read fallout the plan
    // predicted). Un-skip when signing round-trips deterministically.
    [Skip("Pending context-never-null Stage 5: context-ful Store verify-on-read DataHashMismatch (signing canonicalization determinism)")]
    [Test]
    public async Task DictOfTypedEntries_StoreRoundTrip_PreservesNestedTypes()
    {
        await using var app = NewApp();
        var context = app.User.Context;

        // value = dict { description: text, steps: list[ dict{index:number} ] }
        var steps = new global::app.type.list.@this()
            .Add(new global::app.data.@this("", new global::app.type.dict.@this().Set("index", 0L)));
        var value = new global::app.type.dict.@this()
            .Set("description", "a plan")
            .Set("steps", steps);
        var result = global::app.data.@this.Ok(value);
        result.Context = context;

        // The store binds the serializer with its context (application/plang) — the read
        // routes through the one context-ful Typed-reader path, borning each nested
        // {type,value} entry back to its real type instead of a raw dict.
        var serializer = new global::app.channel.serializer.plang.@this(context);
        using var ms = new System.IO.MemoryStream();
        await serializer.SerializeAsync(ms, result, global::app.View.Store);
        ms.Position = 0;
        var back = await serializer.DeserializeAsync<global::app.type.item.@this>(ms, global::app.View.Store);

        await Assert.That(back.Success).IsTrue();
        var dict = back.Peek() as global::app.type.dict.@this;
        await Assert.That(dict).IsNotNull();
        // The bug: steps comes back a {type,value} dict instead of a list.
        await Assert.That(dict!.Get("steps")!.Type?.Name).IsEqualTo("list");
        await Assert.That(dict.Get("description")!.Type?.Name).IsEqualTo("text");
    }
}
