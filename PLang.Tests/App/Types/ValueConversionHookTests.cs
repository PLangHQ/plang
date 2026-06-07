using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangPath = global::app.type.path.@this;
using Image = global::app.type.image.@this;
using Number = global::app.type.number.@this;
using Datetime = global::app.type.datetime.@this;
using Duration = global::app.type.duration.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// Stage 10 — value construction belongs to the types. Each type's <c>Convert</c>
/// hook builds a value of itself; the infra dispatch door (<c>App.Type.Convert</c>)
/// reaches the hook from a CLR target; the residual primitive leaf + plumbing cover
/// the type-agnostic remainder. The locale guard proves the divergent-twin bug is gone.
/// </summary>
public class ValueConversionHookTests
{
    private static (global::app.@this app, global::app.actor.context.@this context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-conv-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir);
        return (app, app.User.Context);
    }

    private record Point(int X, int Y);

    // ---- The locale guard: the bug the divergent twin caused ----

    [Test]
    public async Task TextChannel_TypedDeserialize_IsInvariantCulture_NotCurrentCulture()
    {
        var prior = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            // de-DE reads "," as the decimal separator. The old channel/serializer/Text
            // fork used CurrentCulture, so "3.14" parsed to 314m here. The one converter
            // is invariant — "3.14" is 3.14m regardless of locale.
            System.Threading.Thread.CurrentThread.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("de-DE");

            var serializer = new global::app.channel.serializer.Text();
            var dec = serializer.Deserialize<global::app.type.number.@this>("3.14").Value;
            await Assert.That(dec).IsEqualTo(3.14m);

            var dbl = serializer.Deserialize<global::app.type.number.@this>("3.14").Value;
            await Assert.That(dbl).IsEqualTo(3.14d);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prior;
        }
    }

    // ---- Each domain hook in isolation ----

    [Test]
    public async Task NumberHook_KindPicksPrecision_NullKindDerives()
    {
        var (_, ctx) = MakeApp();
        await Assert.That(Number.Convert("3.14", "decimal", ctx).Value).IsEqualTo(3.14m);
        await Assert.That(Number.Convert("42", "long", ctx).Value).IsEqualTo(42L);
        // null kind → derive from the literal shape (Build): integer → int.
        await Assert.That(Number.Convert("42", null, ctx).Value).IsEqualTo(42);
        // non-numeric → error owned by number.
        await Assert.That(Number.Convert("abc", "int", ctx).Success).IsFalse();
    }

    [Test]
    public async Task DatetimeHook_ParsesIso()
    {
        var (_, ctx) = MakeApp();
        // kind null ⇒ the born-native wrapper (mirrors DurationHook); GetValue projects to raw.
        var v = Datetime.Convert("2024-03-15T10:30:00+00:00", null, ctx).GetValue<System.DateTimeOffset>();
        await Assert.That(v.Year).IsEqualTo(2024);
    }

    [Test]
    public async Task DurationHook_ParsesIsoAndDotNet()
    {
        var (_, ctx) = MakeApp();
        await Assert.That(Duration.Convert("PT30S", null, ctx).GetValue<System.TimeSpan>()).IsEqualTo(System.TimeSpan.FromSeconds(30));
        await Assert.That(Duration.Convert("00:05:00", null, ctx).GetValue<System.TimeSpan>()).IsEqualTo(System.TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task PathHook_ResolvesScheme()
    {
        var (_, ctx) = MakeApp();
        var v = PLangPath.Convert("greeting.txt", null, ctx).Value;
        await Assert.That(v).IsAssignableTo<PLangPath>();
    }

    [Test]
    public async Task ImageHook_PathStringMintsLazyHandle_NoIo()
    {
        var (_, ctx) = MakeApp();
        var v = Image.Convert("photo.png", null, ctx).Value as Image;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.Path).IsNotNull();
        // Lazy: no content read at construction.
        await Assert.That(v.Bytes.Length).IsEqualTo(0);
        // A raw byte[] is declined (built at its own seam) — null, not an error.
        await Assert.That(Image.Convert(new byte[] { 1, 2, 3 }, null, ctx)).IsNull();
    }

    [Test]
    public async Task GoalCallHook_FromBareName()
    {
        var (_, ctx) = MakeApp();
        var v = GoalCall.Convert("MyGoal", null, ctx).Value as GoalCall;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.Name).IsEqualTo("MyGoal");
    }

    // ---- Each hook also reachable through the infra door (the Data.As<T> path) ----

    [Test]
    public async Task InfraDoor_ReachesEveryHook_FromClrTarget()
    {
        var (app, ctx) = MakeApp();

        await Assert.That(app.Type.Convert("3.14", typeof(decimal), ctx).Value).IsEqualTo(3.14m);
        await Assert.That(app.Type.Convert("PT30S", typeof(System.TimeSpan), ctx).Value)
            .IsEqualTo(System.TimeSpan.FromSeconds(30));
        await Assert.That(app.Type.Convert("2024-03-15T10:30:00+00:00", typeof(System.DateTimeOffset), ctx).Value)
            .IsTypeOf<System.DateTimeOffset>();
        await Assert.That(app.Type.Convert("photo.png", typeof(Image), ctx).Value).IsTypeOf<Image>();
        await Assert.That(app.Type.Convert("MyGoal", typeof(GoalCall), ctx).Value).IsTypeOf<GoalCall>();
    }

    [Test]
    public async Task InfraDoor_JsonString_ShapesToRecord()
    {
        var (app, ctx) = MakeApp();
        var v = app.Type.Convert("{\"x\":1,\"y\":2}", typeof(Point), ctx).Value as Point;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.X).IsEqualTo(1);
        await Assert.That(v.Y).IsEqualTo(2);
    }

    // ---- Residual leaf — genuine CLR primitives with no PLang family ----

    [Test]
    public async Task ResidualLeaf_BoolGuidEnum()
    {
        var (app, ctx) = MakeApp();
        await Assert.That(app.Type.Convert("true", typeof(bool), ctx).Value).IsEqualTo(true);

        var g = System.Guid.NewGuid();
        await Assert.That(app.Type.Convert(g.ToString(), typeof(System.Guid), ctx).Value).IsEqualTo(g);

        await Assert.That(app.Type.Convert("Monday", typeof(System.DayOfWeek), ctx).Value)
            .IsEqualTo(System.DayOfWeek.Monday);
    }

    // ---- Plumbing fallback — a hook-less type, non-json source ----

    [Test]
    public async Task PlumbingFallback_StringCtorType_NoHook()
    {
        var (app, ctx) = MakeApp();
        // Uri has no Convert hook and is not a primitive — the generic single-string
        // constructor reflection arm builds it.
        var v = app.Type.Convert("http://example.com/", typeof(System.Uri), ctx).Value as System.Uri;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.Host).IsEqualTo("example.com");
    }
}
