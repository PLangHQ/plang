using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangPath = global::app.type.item.path.@this;
using Image = global::app.type.item.image.@this;
using Number = global::app.type.item.number.@this;
using Datetime = global::app.type.item.datetime.@this;
using Duration = global::app.type.item.duration.@this;

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
        var app = TestApp.Create(dir);
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

            var serializer = new global::app.channel.serializer.Text(global::PLang.Tests.TestApp.SharedContext);
            var dec = (await serializer.Deserialize<global::app.type.item.number.@this>("3.14").Value());
            await Assert.That(dec).IsEqualTo(3.14m);

            var dbl = (await serializer.Deserialize<global::app.type.item.number.@this>("3.14").Value());
            await Assert.That(dbl).IsEqualTo(3.14d);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prior;
        }
    }

    // ---- Each domain hook in isolation ----

    // Each type builds a value of itself through its Create courier; the public
    // conversion door (App.Type.Convert → the courier) is how these are reached from
    // a CLR target. (The per-type static Convert hook is gone — Create is the one door.)

    [Test]
    public async Task NumberConversion_KindPicksPrecision_NullDerives()
    {
        var (app, ctx) = MakeApp();
        await Assert.That((await app.Type.Convert("3.14", typeof(decimal), ctx).Value())?.ToString()).IsEqualTo("3.14");
        await Assert.That((await app.Type.Convert("42", typeof(long), ctx).Value())?.ToString()).IsEqualTo("42");
        await Assert.That((await app.Type.Convert("42", typeof(int), ctx).Value())?.ToString()).IsEqualTo("42");
        // non-numeric → error owned by number.
        await Assert.That(app.Type.Convert("abc", typeof(int), ctx).Success).IsFalse();
    }

    [Test]
    public async Task DatetimeConversion_ParsesIso()
    {
        var (app, ctx) = MakeApp();
        var v = ((global::app.type.item.@this)(await app.Type.Convert("2024-03-15T10:30:00+00:00", typeof(System.DateTimeOffset), ctx).Value())!).Clr<System.DateTimeOffset>();
        await Assert.That(v.Year).IsEqualTo(2024);
    }

    [Test]
    public async Task DurationConversion_ParsesIsoAndDotNet()
    {
        var (app, ctx) = MakeApp();
        await Assert.That(((global::app.type.item.@this)(await app.Type.Convert("PT30S", typeof(System.TimeSpan), ctx).Value())!).Clr<System.TimeSpan>()).IsEqualTo(System.TimeSpan.FromSeconds(30));
        await Assert.That(((global::app.type.item.@this)(await app.Type.Convert("00:05:00", typeof(System.TimeSpan), ctx).Value())!).Clr<System.TimeSpan>()).IsEqualTo(System.TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task PathConversion_ResolvesScheme()
    {
        var (app, ctx) = MakeApp();
        var v = await app.Type.Convert("greeting.txt", typeof(PLangPath), ctx).Value();
        await Assert.That(v).IsAssignableTo<PLangPath>();
    }

    [Test]
    public async Task ImageConversion_PathStringMintsLazyHandle_NoIo()
    {
        var (_, ctx) = MakeApp();
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this("image", null), context: ctx);
        var v = Image.Create("photo.png", carrier) as Image;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.Path).IsNotNull();
        // Lazy: no content read at construction.
        await Assert.That(v.Bytes.Length).IsEqualTo(0);
    }

    [Test]
    public async Task GoalCallConversion_FromBareName()
    {
        var (_, ctx) = MakeApp();
        var v = (await GoalCall.Convert("MyGoal", null, ctx).Value()) as GoalCall;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.Name).IsEqualTo("MyGoal");
    }

    // ---- Each hook also reachable through the infra door (the Type.Convert → Clr<T> path) ----

    [Test]
    public async Task InfraDoor_ReachesEveryHook_FromClrTarget()
    {
        var (app, ctx) = MakeApp();

        await Assert.That((await app.Type.Convert("3.14", typeof(decimal), ctx).Value())?.ToString()).IsEqualTo("3.14");
        // The Data face re-lifts to the born-typed wrapper; the asked-for CLR
        // shape is reached through the wrapper's own Clr exit.
        await Assert.That(((global::app.type.item.@this)(await app.Type.Convert("PT30S", typeof(System.TimeSpan), ctx).Value())!).Clr<System.TimeSpan>())
            .IsEqualTo(System.TimeSpan.FromSeconds(30));
        await Assert.That(((global::app.type.item.@this)(await app.Type.Convert("2024-03-15T10:30:00+00:00", typeof(System.DateTimeOffset), ctx).Value())!).Clr<System.DateTimeOffset>())
            .IsTypeOf<System.DateTimeOffset>();
        await Assert.That((await app.Type.Convert("photo.png", typeof(Image), ctx).Value())).IsTypeOf<Image>();
        await Assert.That((await app.Type.Convert("MyGoal", typeof(GoalCall), ctx).Value())).IsTypeOf<GoalCall>();
    }

    [Test]
    public async Task InfraDoor_JsonString_ShapesToRecord()
    {
        var (app, ctx) = MakeApp();
        var v = Lower<Point>(await app.Type.Convert("{\"x\":1,\"y\":2}", typeof(Point), ctx).Value());
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.X).IsEqualTo(1);
        await Assert.That(v.Y).IsEqualTo(2);
    }

    // ---- Residual leaf — genuine CLR primitives with no PLang family ----

    [Test]
    public async Task ResidualLeaf_BoolGuidEnum()
    {
        var (app, ctx) = MakeApp();
        await Assert.That((await app.Type.Convert("true", typeof(bool), ctx).Value())?.ToString()).IsEqualTo("true");

        var g = System.Guid.NewGuid();
        // guid/enum are items now — only .Clr<T> (via Lower<T>) yields the raw CLR value.
        await Assert.That(Lower<System.Guid>(await app.Type.Convert(g.ToString(), typeof(System.Guid), ctx).Value())).IsEqualTo(g);

        await Assert.That(Lower<System.DayOfWeek>(await app.Type.Convert("Monday", typeof(System.DayOfWeek), ctx).Value())).IsEqualTo(System.DayOfWeek.Monday);
    }

    // ---- Plumbing fallback — a hook-less type, non-json source ----

    [Test]
    public async Task PlumbingFallback_StringCtorType_NoHook()
    {
        var (app, ctx) = MakeApp();
        // Uri has no Convert hook and is not a primitive — the generic single-string
        // constructor reflection arm builds it.
        var v = Lower<System.Uri>(await app.Type.Convert("http://example.com/", typeof(System.Uri), ctx).Value());
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.Host).IsEqualTo("example.com");
    }
}
