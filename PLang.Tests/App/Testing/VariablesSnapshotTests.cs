using global::App.Variables;

namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 4 — Variables.Snapshot().
/// Captures current variable state for assertion-failure diagnostics. Returns a flat
/// Dictionary&lt;string, object?&gt; of user-visible variables at the point of call.
/// Called from assert handlers on failure only (guard: no snapshot on success).
/// System vars (%!app%, Now, etc.) are excluded by default; the renderer can opt in
/// to include specific system vars the user referenced (render-time concern, Batch 11).
/// </summary>
public class VariablesSnapshotTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // Fresh Variables — after filtering system vars (!app, Now, GUID, etc.) — snapshots to an empty dict.
    [Test]
    public async Task Snapshot_EmptyNonSystemVars_ReturnsEmptyDictionary()
    {
        var snapshot = _app.User.Context.Variables.Snapshot();
        await Assert.That(snapshot.Count).IsEqualTo(0);
    }

    // Setting %foo%=1, %bar%="x", %baz%=[1,2,3] — snapshot contains all three with correct values.
    [Test]
    public async Task Snapshot_UserVariables_AllIncludedInDictionary()
    {
        var vars = _app.User.Context.Variables;
        vars.Set("foo", 1);
        vars.Set("bar", "x");
        vars.Set("baz", new List<int> { 1, 2, 3 });

        var snapshot = vars.Snapshot();
        await Assert.That(snapshot.ContainsKey("foo")).IsTrue();
        await Assert.That(snapshot["foo"]).IsEqualTo(1);
        await Assert.That(snapshot["bar"]).IsEqualTo("x");
        await Assert.That(snapshot["baz"]).IsNotNull();
    }

    // Convention: %!app%, %!fileSystem%, Now, GUID, etc. are excluded from the default
    // assertion-failure view. The renderer may opt in to include specific system vars
    // the user referenced in the failing step (see Batch 11).
    [Test]
    public async Task Snapshot_SystemVariables_ExcludedByDefault()
    {
        var snapshot = _app.User.Context.Variables.Snapshot();
        await Assert.That(snapshot.ContainsKey("Now")).IsFalse();
        await Assert.That(snapshot.ContainsKey("NowUtc")).IsFalse();
        await Assert.That(snapshot.ContainsKey("GUID")).IsFalse();
        await Assert.That(snapshot.Keys.Any(k => k.StartsWith("!"))).IsFalse();
    }

    // A variable explicitly set to null is in the dict with value==null — distinguishable
    // from the "never set" case. Matters for the "(null)" vs "(unset)" failure rendering.
    [Test]
    public async Task Snapshot_NullValuedVariable_PresentAsNull_NotAbsent()
    {
        var vars = _app.User.Context.Variables;
        vars.Set("maybe", null);

        var snapshot = vars.Snapshot();
        await Assert.That(snapshot.ContainsKey("maybe")).IsTrue();
        await Assert.That(snapshot["maybe"]).IsNull();
    }

    // A name never set is not in the dict. Architect's example: %result% rendered as "(unset)".
    [Test]
    public async Task Snapshot_UnsetVariable_AbsentFromDictionary()
    {
        var snapshot = _app.User.Context.Variables.Snapshot();
        await Assert.That(snapshot.ContainsKey("neverSet")).IsFalse();
    }

    // Architect spec: snapshot values are by-reference, no deep clone. Mutating a list
    // in the Variables store after snapshot changes the snapshot too. Acceptable because
    // the App is about to be disposed — no further mutations happen after render.
    [Test]
    public async Task Snapshot_CapturesByReference_MutationAfterSnapshotIsReflected()
    {
        var vars = _app.User.Context.Variables;
        var list = new List<int> { 1, 2 };
        vars.Set("items", list);

        var snapshot = vars.Snapshot();
        list.Add(3); // mutate after snapshot

        var captured = (List<int>)snapshot["items"]!;
        await Assert.That(captured.Count).IsEqualTo(3);
    }

    // Nested scope via Save/Restore: outer sets %x%=1, inner sets %x%=2 and snapshots —
    // snapshot shows %x%=2 (inner wins, flat-dict mechanics). Architect's "innermost wins"
    // is trivially satisfied because only one value per name exists at any time.
    [Test]
    public async Task Snapshot_AfterInnerScopeSet_ReflectsMostRecentWrite()
    {
        var vars = _app.User.Context.Variables;
        vars.Set("x", 1);
        var saved = vars.Save();
        try
        {
            vars.Set("x", 2);
            var snapshot = vars.Snapshot();
            await Assert.That(snapshot["x"]).IsEqualTo(2);
        }
        finally { vars.Restore(saved); }
    }

    // Backing is ConcurrentDictionary; Snapshot iteration must not throw while another
    // thread is calling Set. (independent — thread-safety)
    [Test]
    public async Task Snapshot_DuringConcurrentWrite_DoesNotThrow()
    {
        var vars = _app.User.Context.Variables;
        for (int i = 0; i < 100; i++) vars.Set($"k{i}", i);

        using var stop = new CancellationTokenSource();
        var writer = Task.Run(() =>
        {
            var r = new Random(42);
            while (!stop.IsCancellationRequested)
                vars.Set($"k{r.Next(0, 100)}", r.Next());
        });

        // Iterate many times — would throw InvalidOperationException if enumerator wasn't snapshot-style.
        for (int i = 0; i < 1000; i++)
        {
            var snap = vars.Snapshot();
            await Assert.That(snap.Count).IsGreaterThanOrEqualTo(1);
        }

        stop.Cancel();
        await writer;
    }
}
