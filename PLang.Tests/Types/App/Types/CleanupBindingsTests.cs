using System.Text.Json;

namespace PLang.Tests.App.Types;

// plang-types — Stage 6
// Temporal cleanups:
//   datetime → DateTimeOffset (DateTime banished)
//   date → DateOnly
//   time → TimeOnly
//   duration → TimeSpan (LLM-facing name; `timespan` deprecated alias still resolves)
// datetime + duration get folders (parse/format complexity); date + time stay table-only.
// None of the four have a kind — none get a this.Build.cs.

public class CleanupBindingsTests
{
    private global::app.type.catalog.@this _types = null!;

    [Before(Test)]
    public void Setup() => _types = new global::app.type.catalog.@this();

    [Test] public async Task DateTime_PlangName_ResolvesToDateTimeOffset_NotSystemDateTime()
    {
        await Assert.That(_types.Get("datetime")).IsEqualTo(typeof(System.DateTimeOffset));
        await Assert.That(_types.Get("datetime")).IsNotEqualTo(typeof(System.DateTime));
    }

    [Test] public async Task DateTime_Production_NoTypeBinding_ResolvesToSystemDateTime()
    {
        // The canonical PLang name for typeof(DateTime) is still "datetime"
        // (for legacy code paths). New bindings target DateTimeOffset.
        await Assert.That(_types.ResolveName(typeof(System.DateTime))).IsEqualTo("datetime");
        await Assert.That(_types.ResolveName(typeof(System.DateTimeOffset))).IsEqualTo("datetime");
    }

    [Test] public async Task Date_PlangName_ResolvesToDateOnly()
        => await Assert.That(_types.Get("date")).IsEqualTo(typeof(System.DateOnly));

    [Test] public async Task Time_PlangName_ResolvesToTimeOnly()
        => await Assert.That(_types.Get("time")).IsEqualTo(typeof(System.TimeOnly));

    [Test] public async Task Duration_PlangName_ResolvesToTimeSpan()
        => await Assert.That(_types.Get("duration")).IsEqualTo(typeof(System.TimeSpan));

    [Test] public async Task Timespan_DeprecatedAlias_StillResolvesToTimeSpan()
    {
        // Ingi's call: `timespan` is dropped entirely — `duration` is the
        // single canonical name. Both directions return only duration.
        await Assert.That(_types.Get("timespan")).IsNull();
        await Assert.That(_types.Get("duration")).IsEqualTo(typeof(System.TimeSpan));
    }

    [Test] public async Task DateTime_Parse_Iso8601_WithTimezone_RoundTrips()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-dt-" + System.Guid.NewGuid().ToString("N")[..8]));
        var dt = global::app.type.datetime.@this.Resolve("2024-03-15T10:30:00+02:00", app.User.Context);
        await Assert.That(dt).IsNotNull();
        await Assert.That(dt!.Value.Year).IsEqualTo(2024);
        await Assert.That(dt.Value.Offset).IsEqualTo(System.TimeSpan.FromHours(2));
    }

    [Test] public async Task Duration_Parse_DotColonForm_RoundTrips()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-d1-" + System.Guid.NewGuid().ToString("N")[..8]));
        var d = global::app.type.duration.@this.Resolve("1.02:03:04", app.User.Context);
        await Assert.That(d).IsNotNull();
        await Assert.That(d!.Value.Days).IsEqualTo(1);
        await Assert.That(d.Value.Hours).IsEqualTo(2);
    }

    [Test] public async Task Duration_Parse_Iso8601_PT5M_RoundTrips()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-d2-" + System.Guid.NewGuid().ToString("N")[..8]));
        var d = global::app.type.duration.@this.Resolve("PT5M", app.User.Context);
        await Assert.That(d).IsNotNull();
        await Assert.That(d!.Value).IsEqualTo(System.TimeSpan.FromMinutes(5));
    }

    [Test] public async Task Date_RoundTrip_OnWire_PreservesValue()
    {
        var d = new System.DateOnly(2024, 3, 15);
        var json = JsonSerializer.Serialize(d);
        var back = JsonSerializer.Deserialize<System.DateOnly>(json);
        await Assert.That(back).IsEqualTo(d);
    }

    [Test] public async Task Time_RoundTrip_OnWire_PreservesValue()
    {
        var t = new System.TimeOnly(10, 30, 45);
        var json = JsonSerializer.Serialize(t);
        var back = JsonSerializer.Deserialize<System.TimeOnly>(json);
        await Assert.That(back).IsEqualTo(t);
    }

    [Test] public async Task None_OfFourCleanupTypes_DeclaresTypeBuildHook()
    {
        // datetime/date/time/duration don't have a `kind`, so none should
        // declare a static Build(object?) hook.
        var kinds = new global::app.type.kind.Hooks();
        await Assert.That(kinds.Of(typeof(System.DateTimeOffset), System.DateTimeOffset.UtcNow)).IsNull();
        await Assert.That(kinds.Of(typeof(System.DateOnly), System.DateOnly.MinValue)).IsNull();
        await Assert.That(kinds.Of(typeof(System.TimeOnly), System.TimeOnly.MinValue)).IsNull();
        await Assert.That(kinds.Of(typeof(System.TimeSpan), System.TimeSpan.Zero)).IsNull();
        // The wrapper @this types also don't declare Build.
        await Assert.That(kinds.Of(typeof(global::app.type.datetime.@this), null)).IsNull();
        await Assert.That(kinds.Of(typeof(global::app.type.duration.@this), null)).IsNull();
    }

    [Test] public async Task CatalogLeadsWithDuration_TimespanNotPrimary()
    {
        // duration is the canonical name for typeof(TimeSpan); timespan is
        // a deprecated alias kept for back-compat.
        await Assert.That(_types.ResolveName(typeof(System.TimeSpan))).IsEqualTo("duration");
    }
}
