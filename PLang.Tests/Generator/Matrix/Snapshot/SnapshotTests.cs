using PLang.Tests.App.Fixtures;
using App.modules.matrix.snapshot;
using App.modules.matrix.plain;

namespace PLang.Tests.Generator.Matrix.Snapshot;

public class SnapshotOnErrorTests
{
    [Test]
    public async Task SnapshotOnError_ErrorMidRun_AttachesParamsToError()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"a"), ("second", (object?)42) });

        await Assert.That(result.Data.Success).IsFalse();
        await Assert.That(result.Snapshot).IsNotNull();
        await Assert.That(result.Snapshot!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SnapshotOnError_AccessedProperty_FinalValuePresent()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"hello"), ("second", (object?)42) });

        var firstEntry = result.Snapshot!.FirstOrDefault(p => p.Name == "First");
        await Assert.That(firstEntry).IsNotNull();
        await Assert.That(firstEntry!.WasAccessed).IsTrue();
    }

    [Test]
    public async Task SnapshotOnError_UnaccessedProperty_FinalValueNull()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"hello"), ("second", (object?)42) });

        // Second is never read by Run() — snapshot has PrValue but FinalValue=null.
        var secondEntry = result.Snapshot!.FirstOrDefault(p => p.Name == "Second");
        await Assert.That(secondEntry).IsNotNull();
        await Assert.That(secondEntry!.WasAccessed).IsFalse();
        await Assert.That(secondEntry.FinalValue).IsNull();
    }

    [Test]
    public async Task SnapshotOnError_PrValueIsRaw_NoResolution()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"hello-raw"), ("second", (object?)42) });

        var firstEntry = result.Snapshot!.FirstOrDefault(p => p.Name == "First");
        // PrValue is raw — exactly what we passed.
        await Assert.That(firstEntry!.PrValue).IsEqualTo("hello-raw");
    }

    [Test]
    public async Task SnapshotOnError_HandlerSucceeds_NoSnapshotAttached()
    {
        await using var app = new global::App.@this("/app");
        var result = await MatrixRunner.RunAsync<StringPlain>(app,
            parameters: new[] { ("path", (object?)"hello") });
        await Assert.That(result.Data.Success).IsTrue();
        // Snapshot is null on success.
        await Assert.That(result.Snapshot).IsNull();
    }
}
