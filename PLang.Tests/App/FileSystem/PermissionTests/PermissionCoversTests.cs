using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Permission = global::App.FileSystem.Permission.@this;
using Match = global::App.FileSystem.Permission.Match;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Write = global::App.FileSystem.Permission.Verb.Write;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Stage 1 — Batch 2: Permission.Covers wires path match + verb cover; Match
/// dispatch is closed (unknown enum → false); JSON round-trip is lossless.
public class PermissionCoversTests
{
    private static Permission Grant(string path, Match match, Verb? verb = null) =>
        new("user", path, verb ?? Verb.AllowAll(), match);

    private static Permission Request(string path, Verb? verb = null) =>
        new("user", path, verb ?? Verb.AllowAll(), Match.Exact);

    [Test] public async Task ExactMatch_EqualPath_Covers()
    {
        var g = Grant("/apps/Email/file.txt", Match.Exact);
        await Assert.That(g.Covers(Request("/apps/Email/file.txt"))).IsTrue();
    }

    [Test] public async Task ExactMatch_DifferentPath_DoesNotCover()
    {
        var g = Grant("/apps/Email/file.txt", Match.Exact);
        await Assert.That(g.Covers(Request("/apps/Email/other.txt"))).IsFalse();
    }

    [Test] public async Task GlobMatch_PatternCoversConcretePath()
    {
        var g = Grant("/apps/*/file.txt", Match.Glob);
        await Assert.That(g.Covers(Request("/apps/Email/file.txt"))).IsTrue();
    }

    [Test] public async Task GlobMatch_NonMatchingPattern_DoesNotCover()
    {
        var g = Grant("/apps/*/file.txt", Match.Glob);
        await Assert.That(g.Covers(Request("/apps/Email/Sub/file.txt"))).IsFalse();
    }

    [Test] public async Task RegexMatch_PatternCoversConcretePath()
    {
        var g = Grant(@"^/apps/[^/]+/file\.txt$", Match.Regex);
        await Assert.That(g.Covers(Request("/apps/Email/file.txt"))).IsTrue();
    }

    [Test] public async Task RegexMatch_NonMatchingPattern_DoesNotCover()
    {
        var g = Grant(@"^/apps/[^/]+/file\.txt$", Match.Regex);
        await Assert.That(g.Covers(Request("/apps/Email/other.txt"))).IsFalse();
    }

    [Test] public async Task UnknownMatchEnumValue_CoversReturnsFalse_FailClosed()
    {
        var g = Grant("/whatever", (Match)999);
        await Assert.That(g.Covers(Request("/whatever"))).IsFalse();
    }

    [Test] public async Task PathMatches_ButVerbDoesNot_DoesNotCover()
    {
        var grantVerb = new Verb { Write = new Write(Overwrite: false) };
        var g = Grant("/p", Match.Exact, grantVerb);
        await Assert.That(g.Covers(Request("/p"))).IsFalse();
    }

    [Test] public async Task SameRecordShape_GrantRoleAndRequestRole_BothLegible()
    {
        var grant = new Permission("user", "/apps/*/file.txt", Verb.AllowAll(), Match.Glob);
        var request = new Permission("user", "/apps/Email/file.txt", Verb.AllowAll(), Match.Exact);
        await Assert.That(grant.Covers(request)).IsTrue();
    }

    [Test] public async Task JsonRoundTrip_PermissionRecord_RoundTripsEqual()
    {
        var original = new Permission("user", "/p", Verb.AllowAll(), Match.Glob);
        var json = JsonSerializer.Serialize(original);
        var roundtripped = JsonSerializer.Deserialize<Permission>(json);
        await Assert.That(roundtripped).IsEqualTo(original);
    }
}
