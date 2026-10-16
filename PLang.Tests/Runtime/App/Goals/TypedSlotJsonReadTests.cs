namespace PLang.Tests.App.Goals;

// Decision 758 — stage 3's second half: a typed dict slot whose value is a %ref% to an unread json
// value ({item, json}, as a json file read or a json literal makes now — 773/822; json is item's kind).
// The slot's value is the %ref% authored as the builder stamps one (a {text, template} ref), so it
// resolves at settle; on the first read the dict makes itself from the resolved unread json — a use, so
// the raw's format (json) rides the re-declaration (818) and parses it once — and answers its entries.
public class TypedSlotJsonReadTests
{
    [Test]
    public async Task ATypedDictSlot_FirstReadOfTheDict_GoesThroughJsonAndAnswers()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var unread = ctx.App.type.list[new global::app.type.@this("item", "json"), ctx].Make("{\"a\":1}", ctx);
        await ctx.Variable.Set("j", new global::app.data.@this("j", unread, context: ctx));
        var slot = global::PLang.Tests.Shared.Make.Built(ctx, "Data", "%j%")
            .As<global::app.type.item.dict.@this>();

        await slot.Settle();
        var dict = await slot.Value();

        // The first read goes through the json type: the dict answers one entry (the "a" key).
        await Assert.That(dict.Entries(ctx).Count()).IsEqualTo(1);
    }
}
