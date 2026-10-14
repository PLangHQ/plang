namespace PLang.Tests.App.Goals;

// Decision 758 — stage 3's second half: a typed dict slot given an unread json value
// (text with kind json) settles without reading (the coder's companion test
// TypedSlotWholeVariableTests.ATypedSlot_SettlingAReferenceToUnreadContent_DoesNotReadIt
// pins that first half); on the first read of the dict the json type is the one that
// parses, so the dict answers its keys and entries. Today the made value loses json
// and the dict can't be read.
public class TypedSlotJsonReadTests
{
    [Test]
    public async Task ATypedDictSlot_FirstReadOfTheDict_GoesThroughJsonAndAnswers()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var unread = ctx.App.type.list[new global::app.type.@this("text", "json"), ctx].Make("{\"a\":1}", ctx);
        await ctx.Variable.Set("j", new global::app.data.@this("j", unread, context: ctx));
        var slot = ((global::app.data.@this)global::PLang.Tests.Shared.Make.Param(ctx, "Data", "%j%", "dict").value!)
            .As<global::app.type.item.dict.@this>();

        await slot.Settle();
        var dict = await slot.Value();

        // The first read goes through the json type: the dict answers one entry (the "a" key).
        await Assert.That(dict.Entries(ctx).Count()).IsEqualTo(1);
    }
}
