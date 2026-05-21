using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Text.Json;
using Verb = global::app.filesystem.permission.verb.@this;
using Read = global::app.filesystem.permission.verb.Read;
using Write = global::app.filesystem.permission.verb.Write;
using Delete = global::app.filesystem.permission.verb.Delete;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Pins that a narrowed Verb (explicit nulls on non-requested sub-verbs)
/// survives JSON round-trip. Without this, sqlite-backed grants lose their
/// narrowed shape between Set and GetAll, and Covers fails on the next Find.
public class VerbJsonRoundtripTests
{
    [Test] public async Task NarrowedRead_RoundTripsAsNarrow()
    {
        var v = new Verb { Read = new Read(), Write = null, Delete = null };
        var json = JsonSerializer.Serialize(v);
        await Assert.That(json).Contains("\"Write\":null").Or.Contains("\"write\":null");
        var back = JsonSerializer.Deserialize<Verb>(json);
        await Assert.That(back!.Read).IsNotNull();
        await Assert.That(back.Write).IsNull();
        await Assert.That(back.Delete).IsNull();
    }

    [Test] public async Task NarrowedRead_CoversItself()
    {
        var v = new Verb { Read = new Read(), Write = null, Delete = null };
        await Assert.That(v.Covers(v)).IsTrue();
    }

    [Test] public async Task FullyGrantedVerb_CoversNarrowed()
    {
        var grant = Verb.AllowAll(); // default — all three filled
        var request = new Verb { Read = new Read(), Write = null, Delete = null };
        await Assert.That(grant.Covers(request)).IsTrue();
    }
}
