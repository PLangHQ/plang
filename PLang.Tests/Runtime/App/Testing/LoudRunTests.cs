using app.test;

namespace PLang.Tests.App.Tester;

/// <summary>
/// A test run fails loudly. It passes (the process exits 0) only when tests ran and each passed or
/// was deliberately skipped; nothing discovered, a test that could not load, a test that did not
/// run, and a failed test each fail the run (exit 1) with a message saying why. The report
/// artefact is written either way, the unloadable tests in it as errors.
/// </summary>
public class LoudRunTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-loud-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
        _app.User.Channel.Register(new StreamChannel(
            global::app.channel.list.@this.Output, new System.IO.MemoryStream(),
            ChannelDirection.Output, ownsStream: true) { Mime = "text/plain" });
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
    }

    private static global::app.test.@this NewTest(string name, Status status, string? reason = null)
    {
        var test = new global::app.test.@this
        {
            Goal = new Goal { Name = name, Path = global::app.type.item.path.@this.Resolve($"/Tests/{name}.test.goal", TestApp.SharedContext) },
        };
        if (status is Status.Stale or Status.Skipped)
        {
            test.Status = status;
            test.StatusReason = reason;
        }
        else if (status != Status.Ready) test.Complete(status);
        return test;
    }

    [Test]
    public async Task Verdict_NothingDiscovered_Fails()
    {
        var verdict = _app.Test.Verdict(new List<global::app.test.@this>());

        await Assert.That(verdict?.Key).IsEqualTo("NoTestsDiscovered");
    }

    [Test]
    public async Task Verdict_TestsThatCouldNotLoad_AreCountedByReason()
    {
        var old = "old .pr format (\"steps\" is now \"step\") — rebuild it.";
        var verdict = _app.Test.Verdict(new List<global::app.test.@this>
        {
            NewTest("A", Status.Stale, old), NewTest("B", Status.Stale, old),
            NewTest("C", Status.Stale, "no .pr"), NewTest("D", Status.Pass),
        });

        await Assert.That(verdict?.Key).IsEqualTo("TestRunFailed");
        await Assert.That(verdict!.Message).Contains("2 tests could not load: old .pr format (\"steps\" is now \"step\") — rebuild it");
        await Assert.That(verdict.Message).Contains("1 test could not load: no .pr");
    }

    [Test]
    public async Task Verdict_AFailedOrTimedOutTest_Fails()
    {
        var verdict = _app.Test.Verdict(new List<global::app.test.@this>
        {
            NewTest("A", Status.Fail), NewTest("B", Status.Timeout), NewTest("C", Status.Pass),
        });

        await Assert.That(verdict?.Message).IsEqualTo("2 tests failed.");
    }

    [Test]
    public async Task Verdict_ATestThatNeverRan_Fails()
    {
        var verdict = _app.Test.Verdict(new List<global::app.test.@this> { NewTest("A", Status.Ready) });

        await Assert.That(verdict?.Message).IsEqualTo("1 test did not run.");
    }

    [Test]
    public async Task Verdict_AllPassedOrDeliberatelySkipped_Passes()
    {
        var verdict = _app.Test.Verdict(new List<global::app.test.@this>
        {
            NewTest("A", Status.Pass), NewTest("B", Status.Skipped, "tagged 'skip'"),
        });

        await Assert.That(verdict).IsNull();
    }

    // The report fails the run with the verdict — after writing its artefact, which lists the
    // unloadable test.
    [Test]
    public async Task Report_FailedRun_FailsAfterWritingTheArtefact()
    {
        _app.Test.Add(NewTest("A", Status.Pass));
        _app.Test.Add(NewTest("Old", Status.Stale, "no .pr"));

        var result = await new global::app.module.action.test.report(_app.User.Context).Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Key).IsEqualTo("TestRunFailed");
        var artefact = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(_tempDir, ".test", "results.json"));
        await Assert.That(artefact).Contains("Old.test.goal");
        await Assert.That(artefact).Contains("Stale");
    }

    [Test]
    public async Task Report_PassingRun_Succeeds()
    {
        _app.Test.Add(NewTest("A", Status.Pass));

        var result = await new global::app.module.action.test.report(_app.User.Context).Run();

        await result.IsSuccess();
    }

    // In junit a test that could not load is an error — never a quiet skip.
    [Test]
    public async Task JUnit_ATestThatCouldNotLoad_IsAnError()
    {
        var xml = new global::app.test.junit.@this(new List<global::app.test.@this>
        {
            NewTest("Old", Status.Stale, "no .pr"), NewTest("A", Status.Pass),
        }).ToString();

        await Assert.That(xml).Contains("errors=\"1\"");
        await Assert.That(xml).Contains("<error message=\"could not load\">no .pr</error>");
    }
}
