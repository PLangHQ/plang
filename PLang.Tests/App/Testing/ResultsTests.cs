using global::App.Test;

namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 2 — Results collection.
/// Results is the run-wide collection of TestRun entries. Parallel test execution
/// means Add is called concurrently from multiple Tasks — thread-safety is essential.
/// TestRun.Complete transitions the per-test status and records duration.
/// </summary>
public class ResultsTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    private static TestRun NewRun(string name = "T") =>
        new(new TestFile { Path = $"Tests/{name}.test.goal", EntryGoalName = name });

    // Fresh Results starts empty — Count == 0, enumeration yields nothing.
    [Test]
    public async Task NewInstance_Count_IsZero()
    {
        var results = _app.Testing.Results;
        await Assert.That(results.Count).IsEqualTo(0);
        await Assert.That(results.Any()).IsFalse();
    }

    // Adding a TestRun makes it enumerable via the Results collection.
    [Test]
    public async Task Add_TestRun_AppearsInEnumeration()
    {
        var results = _app.Testing.Results;
        var run = NewRun("Alpha");
        results.Add(run);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results.Contains(run)).IsTrue();
    }

    // Mixed Pass/Fail/Timeout/Stale/Skipped TestRuns produce correct per-status counts.
    [Test]
    public async Task Summary_CountsByStatus_AggregatesCorrectly()
    {
        var results = _app.Testing.Results;

        var pass1 = NewRun("P1"); pass1.Complete(TestStatus.Pass);
        var pass2 = NewRun("P2"); pass2.Complete(TestStatus.Pass);
        var fail = NewRun("F"); fail.Complete(TestStatus.Fail);
        var timeout = NewRun("T"); timeout.Complete(TestStatus.Timeout);
        var stale = NewRun("S"); stale.Complete(TestStatus.Stale);
        var skipped = NewRun("K"); skipped.Complete(TestStatus.Skipped);

        results.Add(pass1); results.Add(pass2); results.Add(fail);
        results.Add(timeout); results.Add(stale); results.Add(skipped);

        var summary = results.Summary();

        await Assert.That(summary[TestStatus.Pass]).IsEqualTo(2);
        await Assert.That(summary[TestStatus.Fail]).IsEqualTo(1);
        await Assert.That(summary[TestStatus.Timeout]).IsEqualTo(1);
        await Assert.That(summary[TestStatus.Stale]).IsEqualTo(1);
        await Assert.That(summary[TestStatus.Skipped]).IsEqualTo(1);
    }

    // Parallel test.run appends from multiple Tasks concurrently — Results.Add must be
    // thread-safe: no lost entries, no corruption. Critical for parallel runs.
    [Test]
    public async Task Add_ConcurrentFromMultipleTasks_AllEntriesPreserved()
    {
        var results = _app.Testing.Results;
        const int workers = 16;
        const int perWorker = 100;

        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(() =>
        {
            for (int i = 0; i < perWorker; i++)
                results.Add(NewRun($"W{w}-{i}"));
        })).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(results.Count).IsEqualTo(workers * perWorker);
    }

    // TestRun.Complete(status) sets the terminal status and captures elapsed
    // duration measured from TestRun start.
    [Test]
    public async Task TestRun_Complete_TransitionsStatusAndRecordsDuration()
    {
        var run = NewRun("D");
        await Task.Delay(10); // ensure measurable duration
        run.Complete(TestStatus.Pass);

        await Assert.That(run.Status).IsEqualTo(TestStatus.Pass);
        await Assert.That(run.Duration.TotalMilliseconds).IsGreaterThanOrEqualTo(1);
    }
}
