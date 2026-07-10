using app.test;

namespace PLang.Tests.App.Tester;

/// <summary>
/// The session's test collection (app.Test) — the run-wide set of tests. Parallel
/// test execution means Add is called concurrently from multiple Tasks, so it's
/// thread-safe. A test's Complete transitions its status and records duration.
/// </summary>
public class ResultsTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/test");
    }

    private static global::app.test.@this NewTest(string name = "T") =>
        new(global::PLang.Tests.TestApp.SharedContext)
        {
            Goal = new Goal { Name = name, Path = global::app.type.item.path.@this.Resolve($"/Tests/{name}.test.goal", global::PLang.Tests.TestApp.SharedContext) }
        };

    // Fresh session starts empty — Count == 0, no tests.
    [Test]
    public async Task NewInstance_Count_IsZero()
    {
        await Assert.That(_app.Test.Count).IsEqualTo(0);
        await Assert.That(_app.Test.Tests.Any()).IsFalse();
    }

    // Adding a test makes it enumerable via the session.
    [Test]
    public async Task Add_Test_AppearsInEnumeration()
    {
        var test = NewTest("Alpha");
        _app.Test.Add(test);

        await Assert.That(_app.Test.Count).IsEqualTo(1);
        await Assert.That(_app.Test.Tests.Contains(test)).IsTrue();
    }

    // Mixed Pass/Fail/Timeout/Stale/Skipped tests produce correct per-status counts.
    [Test]
    public async Task Summary_CountsByStatus_AggregatesCorrectly()
    {
        var pass1 = NewTest("P1"); pass1.Complete(global::app.test.Status.Pass);
        var pass2 = NewTest("P2"); pass2.Complete(global::app.test.Status.Pass);
        var fail = NewTest("F"); fail.Complete(global::app.test.Status.Fail);
        var timeout = NewTest("T"); timeout.Complete(global::app.test.Status.Timeout);
        var stale = NewTest("S"); stale.Complete(global::app.test.Status.Stale);
        var skipped = NewTest("K"); skipped.Complete(global::app.test.Status.Skipped);

        _app.Test.Add(pass1); _app.Test.Add(pass2); _app.Test.Add(fail);
        _app.Test.Add(timeout); _app.Test.Add(stale); _app.Test.Add(skipped);

        var summary = _app.Test.Summary();

        await Assert.That(summary[global::app.test.Status.Pass]).IsEqualTo(2);
        await Assert.That(summary[global::app.test.Status.Fail]).IsEqualTo(1);
        await Assert.That(summary[global::app.test.Status.Timeout]).IsEqualTo(1);
        await Assert.That(summary[global::app.test.Status.Stale]).IsEqualTo(1);
        await Assert.That(summary[global::app.test.Status.Skipped]).IsEqualTo(1);
    }

    // Parallel test.run appends from multiple Tasks concurrently — Add must be
    // thread-safe: no lost entries, no corruption. Critical for parallel runs.
    [Test]
    public async Task Add_ConcurrentFromMultipleTasks_AllEntriesPreserved()
    {
        const int workers = 16;
        const int perWorker = 100;

        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(() =>
        {
            for (int i = 0; i < perWorker; i++)
                _app.Test.Add(NewTest($"W{w}-{i}"));
        })).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(_app.Test.Count).IsEqualTo(workers * perWorker);
    }

    // Complete(status) sets the terminal status and captures elapsed duration
    // measured from Start().
    [Test]
    public async Task Test_Complete_TransitionsStatusAndRecordsDuration()
    {
        var test = NewTest("D");
        test.Start();
        await Task.Delay(10); // ensure measurable duration
        test.Complete(global::app.test.Status.Pass);

        await Assert.That(test.Status).IsEqualTo(global::app.test.Status.Pass);
        await Assert.That(test.Duration.TotalMilliseconds).IsGreaterThanOrEqualTo(1);
    }
}
