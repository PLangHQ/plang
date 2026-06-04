using PLang.Tests.App.Fixtures;
using app.module.matrix.snapshot;
using app.module.matrix.plain;

namespace PLang.Tests.Generator.Matrix.Snapshot;

public class SnapshotOnErrorTests
{
    [Test]
    public async Task SnapshotOnError_ErrorMidRun_AttachesParamsToError()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"a"), ("second", (object?)42) });

        await result.Data.IsFailure();
        await Assert.That(result.Snapshot).IsNotNull();
        await Assert.That(result.Snapshot!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SnapshotOnError_AccessedProperty_FinalValuePresent()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"hello"), ("second", (object?)42) });

        var firstEntry = result.Snapshot!.FirstOrDefault(p => p.Name == "First");
        await Assert.That(firstEntry).IsNotNull();
        await Assert.That(firstEntry!.WasAccessed).IsTrue();
    }

    [Test]
    public async Task SnapshotOnError_UnaccessedProperty_FinalValueNull()
    {
        await using var app = new global::app.@this("/app");
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
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"hello-raw"), ("second", (object?)42) });

        var firstEntry = result.Snapshot!.FirstOrDefault(p => p.Name == "First");
        // PrValue is raw — exactly what we passed.
        await Assert.That(firstEntry!.PrValue).IsEqualTo("hello-raw");
    }

    [Test]
    public async Task SnapshotOnError_HandlerSucceeds_NoSnapshotAttached()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<StringPlain>(app,
            parameters: new[] { ("path", (object?)"hello") });
        await result.Data.IsSuccess();
        // Snapshot is null on success.
        await Assert.That(result.Snapshot).IsNull();
    }

    // [Sensitive] on a Data<T> property must mask both PrValue (raw .pr literal) and
    // FinalValue (post-resolution backing value) in the snapshot. Non-sensitive properties
    // on the same handler stay in plaintext.
    [Test]
    public async Task SnapshotOnError_SensitiveProperty_MasksPrValueAndFinalValue()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<SensitiveSnapshot>(app,
            parameters: new[]
            {
                ("apikey", (object?)"sk-live-PLAINTEXT-SECRET"),
                ("endpoint", (object?)"https://api.example.com")
            });

        await result.Data.IsFailure();
        await Assert.That(result.Snapshot).IsNotNull();

        var apiKey = result.Snapshot!.FirstOrDefault(p => p.Name == "ApiKey");
        await Assert.That(apiKey).IsNotNull();
        await Assert.That(apiKey!.PrValue).IsEqualTo("******");
        await Assert.That(apiKey.FinalValue).IsEqualTo("******");
        // The plaintext literal must NOT appear anywhere on the entry.
        await Assert.That(apiKey.PrValue?.ToString()).DoesNotContain("PLAINTEXT");
        await Assert.That(apiKey.FinalValue?.ToString()).DoesNotContain("PLAINTEXT");

        // Non-sensitive sibling stays unmasked.
        var endpoint = result.Snapshot.FirstOrDefault(p => p.Name == "Endpoint");
        await Assert.That(endpoint).IsNotNull();
        await Assert.That(endpoint!.PrValue).IsEqualTo("https://api.example.com");
    }

    // When a [Sensitive] property's .pr value is null, PrValue masks to null (no "******").
    // Pins the null-guard branch on the PrValue side of the emitted snapshot expression —
    // distinguishes 'absent' from 'redacted' for post-mortem analysis.
    [Test]
    public async Task SnapshotOnError_SensitiveProperty_NullPrValue_StaysNull()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<SensitiveSnapshot>(app,
            parameters: new[]
            {
                ("apikey", (object?)null),
                ("endpoint", (object?)"https://api.example.com")
            });

        await result.Data.IsFailure();
        var apiKey = result.Snapshot!.FirstOrDefault(p => p.Name == "ApiKey");
        await Assert.That(apiKey).IsNotNull();
        // PrValue is null in the .pr → mask short-circuits to null (no false "******").
        await Assert.That(apiKey!.PrValue).IsNull();
        // FinalValue null-guard: handler touched .Value but resolved to null — masked to null,
        // not "******". Pins auditor/v1 finding #3.
        await Assert.That(apiKey.FinalValue).IsNull();
    }
}
