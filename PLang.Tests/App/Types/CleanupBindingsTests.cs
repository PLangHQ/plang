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
    [Test] public async Task DateTime_PlangName_ResolvesToDateTimeOffset_NotSystemDateTime()
        => throw new global::System.NotImplementedException();

    [Test] public async Task DateTime_Production_NoTypeBinding_ResolvesToSystemDateTime()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Date_PlangName_ResolvesToDateOnly()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Time_PlangName_ResolvesToTimeOnly()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Duration_PlangName_ResolvesToTimeSpan()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Timespan_DeprecatedAlias_StillResolvesToTimeSpan()
        => throw new global::System.NotImplementedException();

    [Test] public async Task DateTime_Parse_Iso8601_WithTimezone_RoundTrips()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Duration_Parse_DotColonForm_RoundTrips()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Duration_Parse_Iso8601_PT5M_RoundTrips()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Date_RoundTrip_OnWire_PreservesValue()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Time_RoundTrip_OnWire_PreservesValue()
        => throw new global::System.NotImplementedException();

    [Test] public async Task None_OfFourCleanupTypes_DeclaresTypeBuildHook()
        => throw new global::System.NotImplementedException();

    [Test] public async Task CatalogLeadsWithDuration_TimespanNotPrimary()
        => throw new global::System.NotImplementedException();
}
