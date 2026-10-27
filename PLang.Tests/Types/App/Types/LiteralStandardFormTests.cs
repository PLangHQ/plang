namespace PLang.Tests.App.Types;

// A quoted literal in an open `item` slot with no `:type` is typed by its one strict standard form: an ISO datetime
// (with a T), an ISO date, a time (HH:mm or HH:mm:ss), an ISO duration (P…). Anything else stays text. A written
// annotation, or a slot that declares its type, is what it says.
public class LiteralStandardFormTests
{
    private static async Task<string> TypeOf(string line, string prop)
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var step = new global::app.goal.step.@this(app.type.list) { Text = line };
        var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list).Read(line, ctx);
        await read.IsSuccess();
        var action = ((global::app.goal.step.action.list.@this)read.Peek()!).Items().Single();
        return action[prop]!.Type.Name;
    }

    private static Task<string> ValueTypeOf(string literal)
        => TypeOf($"variable.set(Name=%x%, Value={literal})", "Value");

    [Test]
    [Arguments("\"2026-01-01\"", "date")]
    [Arguments("\"2026-01-01T10:00:00\"", "datetime")]
    [Arguments("\"2026-01-01T10:00:00.5Z\"", "datetime")]
    [Arguments("\"2026-01-01T10:00:00+02:00\"", "datetime")]
    [Arguments("\"PT30S\"", "duration")]
    [Arguments("\"P1D\"", "duration")]
    [Arguments("\"10:30\"", "time")]
    [Arguments("\"10:30:15\"", "time")]
    [Arguments("\"2.0\"", "text")]
    [Arguments("\"42\"", "text")]
    [Arguments("\"1st jan 2026\"", "text")]
    [Arguments("\"01/02/2026\"", "text")]
    [Arguments("\"5s\"", "text")]
    [Arguments("\"hello\"", "text")]
    [Arguments("\"P1Y\"", "text")]
    [Arguments("\"2026-01-01 10:00:00\"", "text")]
    public async Task AnUnannotatedLiteralInAnItemSlot_IsTypedByItsStandardForm(string literal, string type)
        => await Assert.That(await ValueTypeOf(literal)).IsEqualTo(type);

    [Test]
    public async Task AnAnnotatedLiteral_IsTheTypeWritten()
        => await Assert.That(await TypeOf("variable.set(Name=%x%, Value:text=\"2026-01-01\")", "Value")).IsEqualTo("text");

    [Test]
    public async Task ADeclaredSlot_KeepsItsType()
        => await Assert.That(await TypeOf("assert.isFalse(Value=%x%, Message=\"2026-01-01\")", "Message")).IsEqualTo("text");
}
