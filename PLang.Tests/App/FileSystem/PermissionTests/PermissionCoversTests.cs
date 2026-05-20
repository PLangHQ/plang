using TUnit.Core;
using Permission = global::App.FileSystem.Permission.@this;
using Match = global::App.FileSystem.Permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Stage 1 — Batch 2: Permission.Covers wires path match + verb cover; Match
/// dispatch is closed (unknown enum → false); JSON round-trip is lossless.
public class PermissionCoversTests
{
    [Test] public Task ExactMatch_EqualPath_Covers()                            { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ExactMatch_DifferentPath_DoesNotCover()                  { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task GlobMatch_PatternCoversConcretePath()                    { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task GlobMatch_NonMatchingPattern_DoesNotCover()              { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task RegexMatch_PatternCoversConcretePath()                   { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task RegexMatch_NonMatchingPattern_DoesNotCover()             { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task UnknownMatchEnumValue_CoversReturnsFalse_FailClosed()    { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task PathMatches_ButVerbDoesNot_DoesNotCover()                { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task SameRecordShape_GrantRoleAndRequestRole_BothLegible()    { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task JsonRoundTrip_PermissionRecord_RoundTripsEqual()         { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
