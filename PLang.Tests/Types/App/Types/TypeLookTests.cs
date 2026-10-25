namespace PLang.Tests.App.Types;

/// <summary>
/// A value looks as its format says, never as the view: a type written in text is the word it goes by (as a .goal
/// writes it), and in json its identity; an action in formal is its call, wherever it sits.
/// </summary>
public class TypeLookTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/type-look-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<string> Encoded(global::app.type.kind.@this format, global::app.type.@this type)
    {
        using var stream = new System.IO.MemoryStream();
        await (await format.Encode(stream, new global::app.data.@this("t", type, context: Ctx), Ctx, global::app.View.Out)).IsSuccess();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Test] public async Task AType_InAJsonResponse_IsItsIdentity()
        => await Assert.That(await Encoded(_app.type.list["item"].kind["json"]!, _app.type.list["text"])).IsEqualTo("{\"name\":\"text\"}");

    [Test] public async Task AType_InText_IsTheWordItGoesBy()
        => await Assert.That(await Encoded(_app.type.list["text"].kind, _app.type.list[new global::app.type.@this("list", "path"), Ctx])).IsEqualTo("list<path>");

    // an action held in another's body writes its own call: the formal writer hands every value back to it
    [Test] public async Task AnActionInABody_WrittenInFormal_IsItsCall()
    {
        var condition = Make.Action(Ctx, "condition", "if", ("Left", "%x%"), ("Operator", "=="), ("Right", 1));
        var body = new Step(_app.type.list) { Text = "write out \"one\"" };
        body.Code.Add(Make.Action(Ctx, "output", "write", ("Data", "one")));
        condition.Child.Add(body);
        var writer = new global::app.goal.step.action.formal.Writer(Ctx);

        await condition.Output(writer);

        await Assert.That(writer.ToString()).StartsWith("condition.if(");
        await Assert.That(writer.ToString()).Contains("{ output.write(");
    }
}
