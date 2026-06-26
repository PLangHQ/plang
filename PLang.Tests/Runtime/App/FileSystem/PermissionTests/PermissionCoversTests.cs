using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Permission = global::app.type.permission.@this;
using Match = global::app.type.permission.Match;
using Verb = global::app.type.permission.Verb;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Batch 2: Permission.Covers wires path match + verb cover; Match
/// dispatch is closed (unknown enum → false); JSON round-trip is lossless.
public class PermissionCoversTests
{
    private static System.Collections.Generic.IReadOnlySet<Verb> Verbs(Verb? verb) =>
        verb is { } v ? new System.Collections.Generic.HashSet<Verb> { v } : Permission.AllVerbs;

    private static Permission Grant(string path, Match match, Verb? verb = null) =>
        new("user", path, Verbs(verb), match);

    private static Permission Request(string path, Verb? verb = null) =>
        new("user", path, Verbs(verb), Match.Exact);

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
        var grantVerb = global::app.type.permission.Verb.Write;
        var g = Grant("/p", Match.Exact, grantVerb);
        await Assert.That(g.Covers(Request("/p"))).IsFalse();
    }

    [Test] public async Task SameRecordShape_GrantRoleAndRequestRole_BothLegible()
    {
        var grant = new Permission("user", "/apps/*/file.txt", global::app.type.permission.@this.AllVerbs, Match.Glob);
        var request = new Permission("user", "/apps/Email/file.txt", global::app.type.permission.@this.AllVerbs, Match.Exact);
        await Assert.That(grant.Covers(request)).IsTrue();
    }

    [Test] public async Task JsonRoundTrip_PermissionRecord_RoundTripsEqual()
    {
        // Permission round-trips through ITS OWN wire (Write/Create via the plang
        // serializer's persistence path), not raw STJ — the grant owns its wire form.
        await using var app = new global::app.@this("/test");
        var ctx = app.User.Context;
        var original = new Permission("user", "/p", global::app.type.permission.@this.AllVerbs, Match.Glob);
        var data = new global::app.data.@this<Permission>("", original, context: ctx);
        var serializer = new global::app.channel.serializer.plang.@this(ctx);
        var stored = serializer.Store(data);
        await stored.IsSuccess();
        var loaded = serializer.Load((await stored.Value())!.ToString()!);
        loaded.Context = ctx;
        var roundtripped = await loaded.Value<Permission>();
        await Assert.That(roundtripped).IsEqualTo(original);
    }
}
