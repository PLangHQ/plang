using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using global::App.FileSystem.Permission;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using Read = global::App.FileSystem.Permission.Verb.Read;
using Write = global::App.FileSystem.Permission.Verb.Write;
using Delete = global::App.FileSystem.Permission.Verb.Delete;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// Stage 1 — Batch 1: Verb sub-records (Read/Write/Delete) default-true options
/// and their Covers logic, plus Verb.@this composition over the three.
public class VerbCoversTests
{
    [Test] public async Task Read_DefaultCtor_AllSubOptionsTrue()
    {
        var r = new Read();
        await Assert.That(r.Recursive).IsTrue();
        await Assert.That(r.Metadata).IsTrue();
    }

    [Test] public async Task Write_DefaultCtor_AllSubOptionsTrue()
    {
        var w = new Write();
        await Assert.That(w.Overwrite).IsTrue();
        await Assert.That(w.Recursive).IsTrue();
    }

    [Test] public async Task Delete_DefaultCtor_AllSubOptionsTrue()
    {
        var d = new Delete();
        await Assert.That(d.Recursive).IsTrue();
    }

    [Test] public async Task ReadCovers_FullGrant_CoversFullRequest()
    {
        await Assert.That(new Read().Covers(new Read())).IsTrue();
    }

    [Test] public async Task ReadCovers_NarrowedGrant_DoesNotCoverFullRequest()
    {
        var grant = new Read(Recursive: false, Metadata: true);
        await Assert.That(grant.Covers(new Read())).IsFalse();
    }

    [Test] public async Task ReadCovers_FullGrant_CoversNarrowedRequest()
    {
        var request = new Read(Recursive: false, Metadata: false);
        await Assert.That(new Read().Covers(request)).IsTrue();
    }

    [Test] public async Task WriteCovers_NarrowedOverwriteGrant_DoesNotCoverFull()
    {
        var grant = new Write(Overwrite: false, Recursive: true);
        await Assert.That(grant.Covers(new Write())).IsFalse();
    }

    [Test] public async Task DeleteCovers_NarrowedRecursiveGrant_DoesNotCoverFull()
    {
        var grant = new Delete(Recursive: false);
        await Assert.That(grant.Covers(new Delete())).IsFalse();
    }

    [Test] public async Task VerbCovers_AllNullGrant_DoesNotCoverAnyVerbRequest()
    {
        var grant = new Verb(); // default: all null
        var request = new Verb { Read = new Read() };
        await Assert.That(grant.Covers(request)).IsFalse();
    }

    [Test] public async Task VerbCovers_AllNullGrant_CoversAllNullRequest()
    {
        // Both empty (no constraints either way) → trivially covered.
        await Assert.That(new Verb().Covers(new Verb())).IsTrue();
    }

    [Test] public async Task VerbCovers_AllowAll_CoversAllowAll()
    {
        await Assert.That(Verb.AllowAll().Covers(Verb.AllowAll())).IsTrue();
    }

    [Test] public async Task VerbCovers_ReadOnlyGrant_DoesNotCoverWriteRequest()
    {
        var grant = new Verb { Read = new Read() };
        var request = new Verb { Write = new Write() };
        await Assert.That(grant.Covers(request)).IsFalse();
    }
}
