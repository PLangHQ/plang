using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// Value construction belongs to the types — each type builds a value of itself
/// through its own <c>Create</c> courier (there is no central conversion door; a
/// value lowers itself via <c>item.Clr</c>, a type builds itself via <c>Create</c>).
/// The per-type string→value coverage lives in the born-native/parse suites
/// (NumberParse, DateWrapper, DurationWrapper, PathTests, BoolWrapper); this file
/// keeps only what those don't: the locale guard and the reference-fundamental hooks.
/// </summary>
public class ValueConversionHookTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static (global::app.@this app, global::app.actor.context.@this context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-conv-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir).Testing();
        return (app, app.actor.list.User.Context);
    }

    // ---- The locale guard: the divergent-twin bug ----

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

            var ctx = app.actor.list.User.Context;
            var text = ctx.Format("text/plain");
            var dec = (await text.Deserialize<global::app.type.item.number.@this>("3.14", ctx).Value());
            await Assert.That(dec).IsEqualTo(3.14m);

            var dbl = (await text.Deserialize<global::app.type.item.number.@this>("3.14", ctx).Value());
            await Assert.That(dbl).IsEqualTo(3.14d);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prior;
        }
    }

    // ---- Reference-fundamental hooks build through their own Create ----

    [Test]
    public async Task ImageConversion_PathStringMintsLazyHandle_NoIo()
    {
        var (_, ctx) = MakeApp();
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this("image", null), context: ctx);
        var v = Image.Create("photo.png", carrier.Type, carrier) as Image;
        await Assert.That(v).IsNotNull();
        await Assert.That(v!.Path).IsNotNull();
        // Lazy: no content read at construction.
        await Assert.That(v.Bytes.Length).IsEqualTo(0);
    }
}
