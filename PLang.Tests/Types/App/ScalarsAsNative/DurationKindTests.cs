using Duration = global::app.type.item.duration.@this;

namespace PLang.Tests.App.ScalarsAsNative;

// A duration's standards are its kinds (short 30s, iso PT30S, dotnet 00:00:30): it is born with the one its text
// is written in — the app's {duration, kind} — and its kind writes it back in it; a span made with no text is
// short; a kind asked for converts it.
public class DurationKindTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    // the value as text writes it out — its Output, never a ToString
    private Task<string> Written(global::app.type.item.@this value) => Ctx.Written(value);

    [Test]
    [Arguments("30s", "short")]
    [Arguments("PT5M", "iso")]
    [Arguments("00:00:30", "dotnet")]
    public async Task EachStandard_KeepsItsText_AndIsItsKind(string text, string kind)
    {
        var duration = Duration.Resolve(text, Ctx)!;

        await Assert.That(await Written(duration)).IsEqualTo(text);
        await Assert.That(((global::app.type.item.@this)duration).Clr<object>()).IsNotNull();
        await Assert.That(new global::app.data.@this("d", duration, context: Ctx).Type.kind.Name).IsEqualTo(kind);
        await Assert.That(ReferenceEquals(((global::app.type.item.@this)duration).Type,
            app.type.list[new global::app.type.@this("duration", kind), Ctx])).IsTrue();
    }

    [Test]
    [Arguments(3600, "1h")]
    [Arguments(90, "90s")]
    [Arguments(1.5, "1500ms")]
    [Arguments(0, "0s")]
    public async Task ASpanMadeWithNoText_IsShort_InItsLargestWholeUnit(double seconds, string text)
        => await Assert.That(await Written(Ctx.Duration(System.TimeSpan.FromSeconds(seconds)))).IsEqualTo(text);

    [Test]
    public async Task ABareNumber_IsNoDurationInAnyStandard()
        => await Assert.That(Duration.Resolve("30", Ctx)).IsNull();

    [Test]
    public async Task TheSameSpanInTwoStandards_IsEqual()
        => await Assert.That(Duration.Resolve("30s", Ctx)!.Equals(Duration.Resolve("PT30S", Ctx))).IsTrue();

    // `as iso`: the declared type names the kind, and the same span is written in it.
    [Test]
    public async Task AKindAskedFor_ConvertsTheSpanToIt()
    {
        var carrier = new global::app.data.@this("d", context: Ctx);
        var declared = new global::app.type.@this("duration", "iso");

        var converted = Duration.Create("30s", declared, carrier)!;

        await Assert.That(await Written(converted)).IsEqualTo("PT30S");
        await Assert.That(converted.Value).IsEqualTo(System.TimeSpan.FromSeconds(30));
    }
}
