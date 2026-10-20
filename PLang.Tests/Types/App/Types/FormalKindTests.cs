using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types;

// formal — a step's actions written as calls — is a format of list: the action list declares it, and it is held as
// {list, formal}, the kind the formal writer writes.
public class FormalKindTests
{
    [Test] public async Task Formal_IsAKindOfList_AndTheFormalWritersKind()
    {
        await using var app = new global::app.@this("/test").Testing();

        var formal = app.type.list["list"].kind["formal"];

        await Assert.That(formal).IsNotNull();
        await Assert.That(formal!.Owner).IsSameReferenceAs(app.type.list["list"]);
        await Assert.That(new global::app.goal.step.action.formal.Writer(app.actor.list.System.Context).Kind).IsSameReferenceAs(formal);
    }

    // In plang a type is its identity and a kind is its name, each through the plang kind's pair for its class.
    [Test] public async Task InPlang_ATypeIsItsIdentity_AKindItsName()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var text = app.type.list["text"];
        var md = text.kind["md"]!;

        await Assert.That(await Plang(text, app, ctx)).IsEqualTo("{\"name\":\"text\"}");
        await Assert.That(await Plang(md, app, ctx)).IsEqualTo("\"md\"");
    }

    private static async Task<string> Plang(global::app.type.item.@this value, global::app.@this app, global::app.actor.context.@this ctx)
    {
        using var ms = new System.IO.MemoryStream();
        await using (var utf8 = new System.Text.Json.Utf8JsonWriter(ms))
        {
            var writer = new global::app.type.item.wire.kind.plang.Writer(utf8, global::app.View.Store, app.actor.list.System.Context);
            await writer.Kind.pair.Output(value, writer, global::app.View.Store, ctx);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
