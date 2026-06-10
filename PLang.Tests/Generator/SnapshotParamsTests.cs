using System.IO;
using System.Linq;
using PLang.Tests.App.Fixtures;
using app.module.matrix.snapshot;
using app.module.matrix.plain;

namespace PLang.Tests.Generator;

// Contract tests for __SnapshotParams generator emission (v4 simplification).

public class SnapshotParamsTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang.Generators")))
            {
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return dir!;
        }
    }

    private static string ReadGenerated(string handlerName)
    {
        var generatedDir = Path.Combine(RepoRoot, "PLang.Tests", "obj", "Debug", "net10.0",
            "generated", "PLang.Generators", "PLang.Generators.this");
        return File.ReadAllText(Path.Combine(generatedDir, handlerName));
    }

    [Test]
    public async Task SnapshotParams_OneEntryPerProperty()
    {
        var snapshotSrc = ReadGenerated("app.module.matrix.snapshot.SnapshotOnError.Action.g.cs");
        // SnapshotOnError has two parameter properties (First, Second). __SnapshotParams should
        // contain two ParamSnapshot entries.
        var entryCount = (snapshotSrc.Length - snapshotSrc.Replace("new global::app.error.ParamSnapshot", "").Length)
            / "new global::app.error.ParamSnapshot".Length;
        await Assert.That(entryCount).IsEqualTo(2);
    }

    [Test]
    public async Task SnapshotEntry_PrValue_FromGetParameterValue()
    {
        var snapshotSrc = ReadGenerated("app.module.matrix.snapshot.SnapshotOnError.Action.g.cs");
        await Assert.That(snapshotSrc).Contains("PrValue = __pr?.Peek()");
    }

    [Test]
    public async Task SnapshotEntry_FinalValue_FromBackingFieldValue()
    {
        var snapshotSrc = ReadGenerated("app.module.matrix.snapshot.SnapshotOnError.Action.g.cs");
        await Assert.That(snapshotSrc).Contains("FinalValue = __First_set ? (object?)__First_backing : null");
    }

    [Test]
    public async Task SnapshotEntry_UnaccessedProperty_PrValueOnly()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"a"), ("second", (object?)42) });

        var second = result.Snapshot!.FirstOrDefault(p => p.Name == "Second");
        await Assert.That(second).IsNotNull();
        // Dispatch resolution binds every parameter before Run() — WasAccessed means
        // "bound at dispatch" and FinalValue carries the resolved Data even when the
        // handler body never touched the property.
        await Assert.That(second!.WasAccessed).IsTrue();
        await Assert.That(second.PrValue?.ToString()).IsEqualTo("42");
        await Assert.That(second.FinalValue).IsNotNull();
    }

    [Test]
    public async Task SnapshotEntry_AccessedProperty_BothPresent_Distinct()
    {
        await using var app = new global::app.@this("/app");
        app.User.Context.Variable.Set("name", "world");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"hello %name%"), ("second", (object?)42) });

        var first = result.Snapshot!.FirstOrDefault(p => p.Name == "First");
        await Assert.That(first).IsNotNull();
        await Assert.That(first!.WasAccessed).IsTrue();
        // PrValue is raw "hello %name%", FinalValue is the resolved Data<global::app.type.text.@this>
        await Assert.That(first.PrValue?.ToString()).IsEqualTo("hello %name%");
        await Assert.That(first.FinalValue).IsNotNull();
    }

    [Test]
    public async Task Snapshot_AttachedToError_OnFailure()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<SnapshotOnError>(app,
            parameters: new[] { ("first", (object?)"a"), ("second", (object?)42) });
        await result.Data.IsFailure();
        await Assert.That(result.Snapshot).IsNotNull();
    }

    [Test]
    public async Task Snapshot_NotAttached_OnSuccess()
    {
        await using var app = new global::app.@this("/app");
        var result = await MatrixRunner.RunAsync<StringPlain>(app,
            parameters: new[] { ("path", (object?)"hello") });
        await result.Data.IsSuccess();
        await Assert.That(result.Snapshot).IsNull();
    }

    [Test]
    public async Task EmitSnapshotEntry_PerPropertyKind_ProducesExpectedFragment()
    {
        // DataProperty.EmitSnapshotEntry produces a non-empty block; ProviderProperty
        // intentionally emits nothing (providers aren't parameter-sourced).
        var providerSrc = ReadGenerated("app.module.matrix.provider.ProviderProp.Action.g.cs");
        // ProviderProp has only a [Code] property — __SnapshotParams body should be
        // empty (just an empty list).
        await Assert.That(providerSrc).Contains("__SnapshotParams()");
        // No ParamSnapshot entry for the Provider property.
        var entryCount = providerSrc.Split(new[] { "new global::app.error.ParamSnapshot" },
            System.StringSplitOptions.None).Length - 1;
        await Assert.That(entryCount).IsEqualTo(0);
    }
}
