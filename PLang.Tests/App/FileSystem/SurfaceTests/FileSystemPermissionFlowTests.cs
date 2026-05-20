using TUnit.Core;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Stage 4 — Batch 8: parametrized coverage across the ~11 FS methods.
/// Each method is exercised under three scenarios:
///   1. In-root path        → Ok with expected value, no Ask issued.
///   2. Out-of-root + Stream channel  → blocking prompt → grant stored → succeeds.
///   3. Out-of-root + Message channel → returns Data&lt;Ask&gt; with Snapshot attached.
///
/// The method enumerable below is the source of `[Arguments]`. Read methods:
/// ReadText, ReadBytes, Exists, List, Stat. Writes: WriteText, WriteBytes,
/// Append, Mkdir. Destructive: Delete. (Move/Copy are exercised by
/// MoveCopyBundledConsentTests — they bundle, so they don't fit the
/// single-path matrix here.)
public class FileSystemPermissionFlowTests
{
    public static System.Collections.Generic.IEnumerable<string> SinglePathMethodNames() =>
        new[] { "ReadText", "ReadBytes", "Exists", "List", "Stat",
                "WriteText", "WriteBytes", "Append", "Mkdir", "Delete" };

    [Test]
    [MethodDataSource(nameof(SinglePathMethodNames))]
    public Task InRootPath_ReturnsOk_NoAskIssued(string method)
    {
        Assert.Fail($"Not implemented for {method}");
        return Task.CompletedTask;
    }

    [Test]
    [MethodDataSource(nameof(SinglePathMethodNames))]
    public Task OutOfRoot_StreamChannel_BlocksAndCompletes_GrantStored(string method)
    {
        Assert.Fail($"Not implemented for {method}");
        return Task.CompletedTask;
    }

    [Test]
    [MethodDataSource(nameof(SinglePathMethodNames))]
    public Task OutOfRoot_MessageChannel_ReturnsDataAsk_WithSnapshot(string method)
    {
        Assert.Fail($"Not implemented for {method}");
        return Task.CompletedTask;
    }
}
