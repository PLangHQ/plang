namespace PLang.Tests.Shared;

/// <summary>
/// Self-diagnosing assertions for <c>test.run</c> results (<see cref="global::app.test.@this"/>).
/// A bare <c>Assert.That(run.Status).IsEqualTo(Pass)</c> collapses on failure to "expected Pass,
/// found Fail" — no WHY, so you end up hand-adding throwaway diagnostic throws to read the reason.
/// These surface the run's StatusReason + Error (key + message) IN the failure text, permanently.
/// Mirrors <c>DataAssertions</c> (which does the same for <c>Data.IsSuccess()</c>).
/// </summary>
public static class TestRunAssertions
{
    /// <summary>Assert a single test run passed — on failure the exception carries the run's name,
    /// status, StatusReason, and Error so the cause reads without instrumenting.</summary>
    public static Task AssertPass(this global::app.test.@this run)
    {
        if (run.Status != global::app.test.Status.Pass)
            throw new System.Exception("expected the test run to Pass but it did not: " + Describe(run));
        return Task.CompletedTask;
    }

    /// <summary>Assert every run in the set passed — on failure names EACH non-passing run with its
    /// reason (not just "one of N failed").</summary>
    public static Task AssertAllPass(this System.Collections.Generic.IEnumerable<global::app.test.@this> runs)
    {
        var bad = runs.Where(r => r.Status != global::app.test.Status.Pass).ToList();
        if (bad.Count > 0)
            throw new System.Exception(
                "expected all runs to Pass; these did not:\n  " + string.Join("\n  ", bad.Select(Describe)));
        return Task.CompletedTask;
    }

    private static string Describe(global::app.test.@this run)
    {
        var reason = run.StatusReason?.ToString();
        return $"{run.Goal?.Name ?? "?"} = {run.Status}"
             + (string.IsNullOrEmpty(reason) ? "" : $" — {reason}")
             + (run.Error == null ? "" : $" [{run.Error.Key}: {run.Error.Message}]");
    }
}
