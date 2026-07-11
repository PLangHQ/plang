using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// date / datetime / time ship an ITypeReader (serializer/Reader.cs) so a raw
// authored literal (`set %x% = "2026-01-01" as date`) materializes through the
// SAME source + reader path the rest of construction uses — not the eager
// Convert hook. Before these readers, `Readers.Reader("date")` threw
// NotSupportedException; these pin existence + a value-identical round-trip via
// the real FromRaw → source → reader path.
public class TemporalReaderTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-temporal-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task Typed_DateDatetimeTime_AreRegistered()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Typed("date", null)).IsNotNull();
        await Assert.That(r.Typed("datetime", null)).IsNotNull();
        await Assert.That(r.Typed("time", null)).IsNotNull();
    }

    [Test] public async Task DateRaw_Materializes_ToDateOnly()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("2026-01-01", type.Create("date", null, context: ctx), ctx, "d");
        var item = await d.Value();
        await Assert.That(item).IsTypeOf<global::app.type.item.date.@this>();
        await Assert.That(((global::app.type.item.date.@this)item!).Clr<System.DateOnly>())
            .IsEqualTo(new System.DateOnly(2026, 1, 1));
    }

    [Test] public async Task DatetimeRaw_Materializes_ToDateTimeOffset()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("2026-01-01T12:30:00+00:00", type.Create("datetime", null, context: ctx), ctx, "dt");
        var item = await d.Value();
        await Assert.That(item).IsTypeOf<global::app.type.item.datetime.@this>();
        await Assert.That(((global::app.type.item.datetime.@this)item!).Clr<System.DateTimeOffset>())
            .IsEqualTo(System.DateTimeOffset.Parse("2026-01-01T12:30:00+00:00",
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test] public async Task TimeRaw_Materializes_ToTimeOnly()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("12:30:00", type.Create("time", null, context: ctx), ctx, "t");
        var item = await d.Value();
        await Assert.That(item).IsTypeOf<global::app.type.item.time.@this>();
        await Assert.That(((global::app.type.item.time.@this)item!).Clr<System.TimeOnly>())
            .IsEqualTo(new System.TimeOnly(12, 30, 0));
    }

    [Test] public async Task BadDateRaw_Fails_AsMaterializeFailed()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("not-a-date", type.Create("date", null, context: ctx), ctx, "bad");
        await d.Value();
        await Assert.That(d.Success).IsFalse();
        await Assert.That(d.Error!.Key).IsEqualTo("MaterializeFailed");
    }
}
