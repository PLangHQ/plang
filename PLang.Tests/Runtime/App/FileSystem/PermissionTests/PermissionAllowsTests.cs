using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Permission = global::app.type.item.permission.@this;
using Match = global::app.type.item.permission.Match;
using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Permission.Allows wires path match + verb containment; Match
/// dispatch is closed (unknown enum → false); JSON round-trip is lossless.
public class PermissionAllowsTests
{
    private static System.Collections.Generic.IReadOnlySet<Verb> Verbs(Verb? verb) =>
        verb is { } v ? new System.Collections.Generic.HashSet<Verb> { v } : Permission.AllVerbs;

    private static Permission Grant(string path, Match match, Verb? verb = null) =>
        new("user", path, Verbs(verb), match);

    private static Permission Request(string path, Verb? verb = null) =>
        new("user", path, Verbs(verb), Match.exact);

    [Test] public async Task ExactMatch_EqualPath_Allows()
    {
        var g = Grant("/apps/Email/file.txt", Match.exact);
        await Assert.That(g.Allows(Request("/apps/Email/file.txt"))).IsTrue();
    }

    [Test] public async Task ExactMatch_DifferentPath_DoesNotAllow()
    {
        var g = Grant("/apps/Email/file.txt", Match.exact);
        await Assert.That(g.Allows(Request("/apps/Email/other.txt"))).IsFalse();
    }

    [Test] public async Task GlobMatch_PatternAllowsConcretePath()
    {
        var g = Grant("/apps/*/file.txt", Match.glob);
        await Assert.That(g.Allows(Request("/apps/Email/file.txt"))).IsTrue();
    }

    [Test] public async Task GlobMatch_NonMatchingPattern_DoesNotAllow()
    {
        var g = Grant("/apps/*/file.txt", Match.glob);
        await Assert.That(g.Allows(Request("/apps/Email/Sub/file.txt"))).IsFalse();
    }

    [Test] public async Task UnknownMatchEnumValue_AllowsReturnsFalse_FailClosed()
    {
        var g = Grant("/whatever", (Match)999);
        await Assert.That(g.Allows(Request("/whatever"))).IsFalse();
    }

    [Test] public async Task PathMatches_ButVerbDoesNot_DoesNotAllow()
    {
        var grantVerb = global::app.type.item.permission.Verb.write;
        var g = Grant("/p", Match.exact, grantVerb);
        await Assert.That(g.Allows(Request("/p"))).IsFalse();
    }

    [Test] public async Task SameRecordShape_GrantRoleAndRequestRole_BothLegible()
    {
        var grant = new Permission("user", "/apps/*/file.txt", global::app.type.item.permission.@this.AllVerbs, Match.glob);
        var request = new Permission("user", "/apps/Email/file.txt", global::app.type.item.permission.@this.AllVerbs, Match.exact);
        await Assert.That(grant.Allows(request)).IsTrue();
    }

    [Test] public async Task JsonRoundTrip_PermissionRecord_RoundTripsEqual()
    {
        // Permission round-trips through ITS OWN wire (Write/Create via the plang
        // serializer's persistence path), not raw STJ — the grant owns its wire form.
        await using var app = new global::app.@this("/test").TestSigning();
        var ctx = app.actor.list.User.Context;
        var original = new Permission("user", "/p", global::app.type.item.permission.@this.AllVerbs, Match.glob);
        var data = new global::app.data.@this<Permission>("", original, context: ctx);
        var plang = ctx.Format("application/plang");
        var stored = plang.Store(data, ctx);
        await stored.IsSuccess();
        var loaded = await plang.Decode(System.Text.Encoding.UTF8.GetBytes((await stored.Value())!.ToString()!), ctx, view: global::app.View.Store);
        var roundtripped = await loaded.Value<Permission>();
        await Assert.That(roundtripped).IsEqualTo(original);
    }
}
