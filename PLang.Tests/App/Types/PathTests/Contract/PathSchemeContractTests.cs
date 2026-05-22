using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System;
using System.Threading.Tasks;
using Path = global::app.types.path.@this;

namespace PLang.Tests.App.Types.PathTests.Contract;

/// <summary>
/// Stage 7 — the generic contract base every scheme handler runs through. Verb
/// round-trips, Permission gating, and failure-shape uniformity asserted once,
/// applied to every scheme via a one-line closed subclass.
///
/// Authorization is driven by registering a <see cref="CannedAnswerChannel"/>
/// on the path's actor — "a" for the authorized tests, "n" for the gate tests.
/// Fixtures mint OUT-OF-ROOT paths so the gate fires uniformly for every scheme.
/// </summary>
public abstract class PathSchemeContractTests<TFixture> : IDisposable
    where TFixture : IPathSchemeFixture, new()
{
    protected TFixture Fixture { get; } = new();

    /// <summary>Registers the "allow" channel on the path's actor and returns the path.</summary>
    private static Path Authorized(Path p)
    {
        p.Context!.Actor!.Channels.Register(new CannedAnswerChannel("a"));
        return p;
    }

    /// <summary>Registers the "deny" channel on the path's actor and returns the path.</summary>
    private static Path Denied(Path p)
    {
        p.Context!.Actor!.Channels.Register(new CannedAnswerChannel("n"));
        return p;
    }

    [Test] public async Task ReadText_Returns_What_WriteText_Wrote()
    {
        var p = Authorized(await Fixture.CreateFresh());
        try
        {
            var content = $"contract {Guid.NewGuid()}";
            var written = await p.WriteText(content);
            await Assert.That(written.Success).IsTrue();
            var read = await p.ReadText();
            await Assert.That(read.Success).IsTrue();
            await Assert.That(read.Value).IsEqualTo(content);
        }
        finally { await Fixture.Cleanup(p); }
    }

    [Test] public async Task Exists_Reflects_Lifecycle()
    {
        var p = Authorized(await Fixture.CreateFresh());
        try
        {
            var before = await p.ExistsAsync();
            await Assert.That(before.Value).IsEqualTo(false);
            await p.WriteText("now here");
            var after = await p.ExistsAsync();
            await Assert.That(after.Value).IsEqualTo(true);
            await p.Delete();
            var gone = await p.ExistsAsync();
            await Assert.That(gone.Value).IsEqualTo(false);
        }
        finally { await Fixture.Cleanup(p); }
    }

    [Test] public async Task Stat_Length_Matches_Written_Bytes()
    {
        if (!Fixture.CanPerform(VerbName.Stat)) return;
        var p = Authorized(await Fixture.CreateFresh());
        try
        {
            await p.WriteText("1234567");
            var stat = await p.Stat();
            await Assert.That(stat.Success).IsTrue();
            var info = (Path.StatInfo)stat.Value!;
            await Assert.That(info.Length).IsEqualTo(7L);
        }
        finally { await Fixture.Cleanup(p); }
    }

    [Test] public async Task CopyTo_Same_Scheme_RoundTrips()
    {
        if (!Fixture.CanPerform(VerbName.CopyTo)) return;
        var src = Authorized(await Fixture.CreateFresh());
        var dst = Authorized(await Fixture.CreateFresh());
        try
        {
            await src.WriteText("copy me");
            var copied = await src.CopyTo(dst, overwrite: true, includeSubfolders: true);
            await Assert.That(copied.Success).IsTrue();
            var read = await dst.ReadText();
            await Assert.That(read.Value).IsEqualTo("copy me");
            var srcStill = await src.ExistsAsync();
            await Assert.That(srcStill.Value).IsEqualTo(true);
        }
        finally { await Fixture.Cleanup(src); await Fixture.Cleanup(dst); }
    }

    [Test] public async Task MoveTo_Is_CopyTo_Plus_Delete()
    {
        if (!Fixture.CanPerform(VerbName.MoveTo)) return;
        var src = Authorized(await Fixture.CreateFresh());
        var dst = Authorized(await Fixture.CreateFresh());
        try
        {
            await src.WriteText("move me");
            var moved = await src.MoveTo(dst, overwrite: true);
            await Assert.That(moved.Success).IsTrue();
            var read = await dst.ReadText();
            await Assert.That(read.Value).IsEqualTo("move me");
            var srcGone = await src.ExistsAsync();
            await Assert.That(srcGone.Value).IsEqualTo(false);
        }
        finally { await Fixture.Cleanup(src); await Fixture.Cleanup(dst); }
    }

    [Test] public async Task Unauthorized_Read_Hits_Permission_Gate()
    {
        var p = Denied(await Fixture.CreateFresh());
        try
        {
            var read = await p.ReadText();
            await Assert.That(read.Success).IsFalse();
            await Assert.That(read.Error).IsTypeOf<global::app.errors.PermissionDenied>();
        }
        finally { await Fixture.Cleanup(p); }
    }

    [Test] public async Task Unauthorized_Write_Hits_Permission_Gate()
    {
        var p = Denied(await Fixture.CreateFresh());
        try
        {
            var write = await p.WriteText("should not land");
            await Assert.That(write.Success).IsFalse();
            await Assert.That(write.Error).IsTypeOf<global::app.errors.PermissionDenied>();
        }
        finally { await Fixture.Cleanup(p); }
    }

    [Test] public async Task Failure_Shape_Is_Uniform_Across_Schemes()
    {
        var p = Denied(await Fixture.CreateFresh());
        try
        {
            var read = await p.ReadText();
            await Assert.That(read.Success).IsFalse();
            // Every scheme routes refusal through the same base Authorize gate —
            // the Error is a PermissionDenied with the same key/status.
            await Assert.That(read.Error!.Key).IsEqualTo("PermissionDenied");
            await Assert.That(read.Error!.StatusCode).IsEqualTo(403);
        }
        finally { await Fixture.Cleanup(p); }
    }

    public void Dispose()
    {
        if (Fixture is IDisposable d) d.Dispose();
    }
}
