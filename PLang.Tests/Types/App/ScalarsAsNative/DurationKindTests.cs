using Duration = global::app.type.item.duration.@this;

namespace PLang.Tests.App.ScalarsAsNative;

// A duration's standards are its kinds (short 30s, iso PT30S, dotnet 00:00:30): it is born with the one its text
// is written in — the app's {duration, kind} — and its kind writes it back in it; a span made with no text is
// short; a kind asked for converts it.
public class DurationKindTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
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
    [Arguments(90, "1m30s")]
    [Arguments(1.5, "1.5s")]
    [Arguments(0.2, "200ms")]
    [Arguments(30, "30s")]
    [Arguments(300, "5m")]
    [Arguments(172800, "2d")]
    [Arguments(183600, "2d3h")]
    [Arguments(83.456, "1m23.456s")]
    [Arguments(0, "0s")]
    public async Task ASpanMadeWithNoText_IsShort_InEachOfItsUnits(double seconds, string text)
        => await Assert.That(await Written(Ctx.Duration(System.TimeSpan.FromSeconds(seconds)))).IsEqualTo(text);

    [Test]
    public async Task ABareNumber_IsNoDurationInAnyStandard()
        => await Assert.That(Duration.Resolve("30", Ctx)).IsNull();

    [Test]
    public async Task TheSameSpanInTwoStandards_IsEqual()
        => await Assert.That(Duration.Resolve("30s", Ctx)!.Equals(Duration.Resolve("PT30S", Ctx))).IsTrue();

    // `as iso`: the declared type names the kind, and a span is made by it, written in it.
    [Test]
    public async Task AKindAskedFor_ConvertsTheSpanToIt()
    {
        var carrier = new global::app.data.@this("d", context: Ctx);
        var declared = new global::app.type.@this("duration", "iso");

        var converted = Duration.Create(System.TimeSpan.FromSeconds(30), declared, carrier)!;

        await Assert.That(await Written(converted)).IsEqualTo("PT30S");
        await Assert.That(converted.Value).IsEqualTo(System.TimeSpan.FromSeconds(30));
    }

    // a measured span, as a step's run is timed: each of its units, its seconds with their fraction
    [Test]
    public async Task AMeasuredSpan_WritesEachOfItsUnits()
        => await Assert.That(await Written(Duration.Create(System.TimeSpan.Parse("00:01:23.456", System.Globalization.CultureInfo.InvariantCulture), Ctx)))
            .IsEqualTo("1m23.456s");

    // the short form reads each of its units, summed
    [Test]
    [Arguments("1h30m", 90 * 60.0)]
    [Arguments("1m23.456s", 83.456)]
    [Arguments("2d3h", 183600.0)]
    public async Task AShortText_ReadsEachOfItsUnits(string text, double seconds)
        => await Assert.That(Duration.Resolve(text, Ctx)!.Value).IsEqualTo(System.TimeSpan.FromSeconds(seconds));

    // a span with no form of its own is made in the standard the setting names
    [Test]
    public async Task TheSetting_NamesTheStandardOfASpanWithNoForm()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var set = await new global::app.type.item.variable.parser.@this("%!app.type.duration.setting.standard%", context.App.type.list).Variable.Single()
            .Set(context.Ok(context.Text("iso")), context);
        await set.IsSuccess();

        var span = Duration.Create(System.TimeSpan.Parse("00:01:23.456", System.Globalization.CultureInfo.InvariantCulture), context);

        await Assert.That(await context.Written(span)).IsEqualTo("PT1M23.456S");
    }
}
