using Duration = global::app.type.item.duration.@this;

namespace PLang.Tests.App.ScalarsAsNative;

// A duration's standards are its kinds (short 30s, iso PT30S, dotnet 00:00:30): it is born with the one its text
// is written in and writes back in it; a span made with no text is short; a kind asked for converts it.
public class DurationKindTests
{
    [Test]
    [Arguments("30s", "short")]
    [Arguments("PT5M", "iso")]
    [Arguments("00:00:30", "dotnet")]
    public async Task EachStandard_KeepsItsText_AndIsItsKind(string text, string kind)
    {
        var duration = Duration.Resolve(text, null!)!;

        await Assert.That(duration.ToString()).IsEqualTo(text);
        await Assert.That(duration.Text).IsEqualTo(text);
        await Assert.That(((global::app.type.item.@this)duration).Clr<object>()).IsNotNull();
        await Assert.That(new global::app.data.@this("d", duration).Type.kind.Name).IsEqualTo(kind);
    }

    [Test]
    [Arguments(3600, "1h")]
    [Arguments(90, "90s")]
    [Arguments(1.5, "1500ms")]
    [Arguments(0, "0s")]
    public async Task ASpanMadeWithNoText_IsShort_InItsLargestWholeUnit(double seconds, string text)
        => await Assert.That(new Duration(System.TimeSpan.FromSeconds(seconds)).Text).IsEqualTo(text);

    [Test]
    public async Task ABareNumber_IsNoDurationInAnyStandard()
        => await Assert.That(Duration.Resolve("30", null!)).IsNull();

    [Test]
    public async Task TheSameSpanInTwoStandards_IsEqual()
        => await Assert.That(Duration.Resolve("30s", null!)!.Equals(Duration.Resolve("PT30S", null!))).IsTrue();

    // `as iso`: the declared type names the kind, and the same span is written in it.
    [Test]
    public async Task AKindAskedFor_ConvertsTheSpanToIt()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var carrier = new global::app.data.@this("d", context: context);
        var declared = new global::app.type.@this("duration", "iso");

        var converted = Duration.Create("30s", declared, carrier)!;

        await Assert.That(converted.Text).IsEqualTo("PT30S");
        await Assert.That(converted.Value).IsEqualTo(System.TimeSpan.FromSeconds(30));
    }
}
