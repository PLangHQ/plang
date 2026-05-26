using app.tester;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 2 — Results collection.
/// Results is the run-wide collection of global::app.tester.Run entries. Parallel test execution
/// means Add is called concurrently from multiple Tasks — thread-safety is essential.
/// global::app.tester.Run.Complete transitions the per-test status and records duration.
/// </summary>
public class ResultsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/test");
    }

    private static global::app.tester.Run NewRun(string name = "T") =>
        new(new global::app.tester.File { Goal = new Goal { Name = name, Path = $"/Tests/{name}.test.goal" } });

    // Fresh Results starts empty — Count == 0, enumeration yields nothing.
    [Test]
    public async Task NewInstance_Count_IsZero()
    {
        var results = _app.Tester.Results;
        await Assert.That(results.Count).IsEqualTo(0);
        await Assert.That(results.Any()).IsFalse();
    }

    // Adding a global::app.tester.Run makes it enumerable via the Results collection.
    [Test]
    public async Task Add_TestRun_AppearsInEnumeration()
    {
        var results = _app.Tester.Results;
        var run = NewRun("Alpha");
        results.Add(run);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results.Contains(run)).IsTrue();
    }

    // Mixed Pass/Fail/Timeout/Stale/Skipped TestRuns produce correct per-status counts.
    [Test]
    public async Task Summary_CountsByStatus_AggregatesCorrectly()
    {
        var results = _app.Tester.Results;

        var pass1 = NewRun("P1"); pass1.Complete(global::app.tester.Status.Pass);
        var pass2 = NewRun("P2"); pass2.Complete(global::app.tester.Status.Pass);
        var fail = NewRun("F"); fail.Complete(global::app.tester.Status.Fail);
        var timeout = NewRun("T"); timeout.Complete(global::app.tester.Status.Timeout);
        var stale = NewRun("S"); stale.Complete(global::app.tester.Status.Stale);
        var skipped = NewRun("K"); skipped.Complete(global::app.tester.Status.Skipped);

        results.Add(pass1); results.Add(pass2); results.Add(fail);
        results.Add(timeout); results.Add(stale); results.Add(skipped);

        var summary = results.Summary();

        await Assert.That(summary[global::app.tester.Status.Pass]).IsEqualTo(2);
        await Assert.That(summary[global::app.tester.Status.Fail]).IsEqualTo(1);
        await Assert.That(summary[global::app.tester.Status.Timeout]).IsEqualTo(1);
        await Assert.That(summary[global::app.tester.Status.Stale]).IsEqualTo(1);
        await Assert.That(summary[global::app.tester.Status.Skipped]).IsEqualTo(1);
    }

    // Parallel test.run appends from multiple Tasks concurrently — Results.Add must be
    // thread-safe: no lost entries, no corruption. Critical for parallel runs.
    [Test]
    public async Task Add_ConcurrentFromMultipleTasks_AllEntriesPreserved()
    {
        var results = _app.Tester.Results;
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

    // global::app.tester.Run.Complete(status) sets the terminal status and captures elapsed
    // duration measured from global::app.tester.Run start.
    [Test]
    public async Task TestRun_Complete_TransitionsStatusAndRecordsDuration()
    {
        var run = NewRun("D");
        await Task.Delay(10); // ensure measurable duration
        run.Complete(global::app.tester.Status.Pass);

        await Assert.That(run.Status).IsEqualTo(global::app.tester.Status.Pass);
        await Assert.That(run.Duration.TotalMilliseconds).IsGreaterThanOrEqualTo(1);
    }
}
