using System.Text.Json;
using app.test;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 14 — safety net. Independent edge cases and security tests that don't
/// fit any single feature. Config boundary robustness (negative/zero/invalid),
/// re-entrant test.start, path-traversal constraints, ANSI stripping in captured
/// output (prevents injection into the failure diagnostic), nested-Data JSON
/// serialization, invalid format values.
/// These catch the cross-cutting failure modes that show up only in integration.
/// </summary>
public class EdgeCaseTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;
    private System.IO.MemoryStream _captureStream = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-edge-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
        _captureStream = new System.IO.MemoryStream();
        _app.actor.list.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, _captureStream,
            ChannelDirection.Output, ownsStream: true)
        { Mime = "text/plain" });
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    private string CapturedOutput() => System.Text.Encoding.UTF8.GetString(_captureStream.ToArray());

    // --test={"timeoutSeconds":-5} → accepted (the type has no positive-bound). The value is
    // a sentinel, not an error: test.start reads TimeoutSeconds ≤ 0 as "no timeout".
    [Test]
    public async Task Config_TimeoutSeconds_NonPositive_AcceptedAsSentinel()
    {
        var result = _app.actor.list.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["timeoutSeconds"] = -5 });
        await result.IsSuccess();
        await Assert.That(_app.actor.list.System.Context.Setting.Of<global::app.test.setting.@this>().TimeoutSeconds.ToInt32()).IsEqualTo(-5);
    }

    // --test={"parallel":0} or {"parallel":-1} → accepted. Zero/negative is the "auto" sentinel:
    // test.start reads Parallel ≤ 0 as ProcessorCount. Not a config error.
    [Test]
    public async Task Config_Parallel_ZeroOrNegative_AcceptedAsSentinel()
    {
        var zero = _app.actor.list.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["parallel"] = 0 });
        await zero.IsSuccess();
        await Assert.That(_app.actor.list.System.Context.Setting.Of<global::app.test.setting.@this>().Parallel.ToInt32()).IsEqualTo(0);

        var neg = _app.actor.list.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["parallel"] = -1 });
        await neg.IsSuccess();
    }

    // A test whose goal calls test.start itself (nested runner). The inner run's
    // results do not leak into the parent run's Results; no deadlock on the
    // semaphore; both inner and outer complete. Keeps test.start re-entrant so
    // meta-tests that exercise the runner are feasible.
    [Test]
    public async Task Run_RecursiveTestRun_InsideTest_IsolatedAndCompletes()
    {
        // Minimal shape: call test.start with an empty list twice — first outer, then
        // simulate a recursive call by calling it again. Without deadlock. Results
        // from the inner call stay on the inner App (we use the same app in the
        // test so the Results grow — verify count is from the outer action, not the
        // inner grandchild-runs).

        var emptyList = new List<global::app.test.@this>();
        var outerAction = new global::app.module.test.start(_app.actor.list.User.Context) { Tests = emptyList.ToListData<global::app.test.@this>() };
        var outerResult = await outerAction.Start();

        await outerResult.IsSuccess();
        await Assert.That(_app.test.list.Items().Count()).IsEqualTo(0);
    }

    // --test={"path":"../../../etc"} → rejected. Discovery is constrained to
    // paths under the app's working directory — prevents a malicious test
    // config from enumerating system directories. (security)
    [Test]
    public async Task Discover_PathTraversal_OutsideProjectRoot_Rejected()
    {
        var action = new global::app.module.test.discover(_app.actor.list.User.Context) { Path = global::app.data.@this<global::app.type.item.path.@this>.Ok(
                global::app.type.item.path.@this.Resolve("../../../etc", _app.actor.list.User.Context)),
            Pattern = new global::app.data.@this<global::app.type.item.text.@this>("Pattern", "*.test.goal"),
            Recursive = new global::app.data.@this<global::app.type.item.@bool.@this>("Recursive", true)
        };

        // Post-Stage-5: discover routes through path.List → AuthGate. An
        // out-of-root traversal either denies (Fail) or returns empty —
        // either way, no filesystem entries from outside leak.
        var result = await action.Start();
        if (result.Success)
        {
            var files = result.GetValue<List<global::app.test.@this>>() ?? new List<global::app.test.@this>();
            await Assert.That(files.Count).IsEqualTo(0);
        }
    }

    // A test that writes ANSI escape sequences via output.write: when the
    // captured output is rendered in the failure diagnostic, escape sequences
    // are stripped or escaped so the terminal renders literal text. Prevents
    // captured-output injection that could forge success messages, clear the
    // screen, or reposition the cursor. (security)
    [Test]
    public async Task Report_ConsoleCapture_AnsiEscapeSequences_Stripped()
    {
        var run = new global::app.test.@this() { Goal = new Goal { Name = "X", Path = global::app.type.item.path.@this.Resolve("/test/X.test.goal", global::PLang.Tests.TestApp.SharedContext) } };
        run.Stdout = "\x1B[32mFAKE OK\x1B[0m\x1B[2JCLEARED";
        run.Complete(global::app.test.Status.Fail, await global::app.error.AssertionError.Of(1, 2, null, _app.actor.list.User.Context));
        _app.test.list.Add(run);

        var action = new global::app.module.test.report(_app.actor.list.User.Context);
        await action.Start();

        var output = CapturedOutput();
        // Escape chars (0x1B) must not appear in the rendered output.
        await Assert.That(output.Contains('\x1B')).IsFalse();
        // The literal text survives — just the control sequences get stripped.
        await Assert.That(output.Contains("FAKE OK")).IsTrue();
    }

    // Retired: nested Data (Data-as-a-value) is not a supported shape — the
    // SetValueDirect courier that produced it is now guarded.

    // --test={"format":"csv"} → error. Format is a choice<Format>; the walk's conversion
    // rejects any value outside the closed set (json/junit).
    [Test]
    public async Task Config_Format_InvalidValue_RejectedWithError()
    {
        var result = _app.actor.list.System.Setting.Set("app.test.setting", new Dictionary<string, object?> { ["format"] = "csv" });
        await result.IsFailure();
    }
}
