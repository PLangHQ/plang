namespace PLang.Tests.App.VariablesTests;

public class VariablesSnapshotTests
{
    [Test]
    public async Task Variables_RoundTrip_PreservesValuesAndProperties_ForUserVars()
    {
        // Set %x%=1 and %obj%={a:1}; Capture; Restore into fresh Variables; deep-equal.
        var src = new global::app.@this("/src");
        src.User.Context.Variable.Set("x", 1);
        src.User.Context.Variable.Set("obj", new Dictionary<string, object?> { ["a"] = 1 });

        var snap = src.Snapshot();
        var dst = new global::app.@this("/dst");
        dst.Restore(snap, dst.User.Context);

        var x = dst.User.Context.Variable.Get("x");
        await Assert.That(x).IsNotNull();
        await Assert.That(x!.Value).IsEqualTo(1);

        var obj = dst.User.Context.Variable.Get("obj");
        await Assert.That(obj).IsNotNull();
        var dict = obj!.Value as IDictionary<string, object?>;
        await Assert.That(dict).IsNotNull();
        await Assert.That(dict!["a"]).IsEqualTo(1);
    }

    [Test]
    public async Task Variables_Snapshot_ExcludesBangPrefixedAndDynamicData()
    {
        // Existing partition: skip !-prefix, DynamicData (Now/GUID/!app/MyIdentity).
        // Settings is now a navigable resolver (not in _variables) so it's absent
        // by construction — no special-case needed.
        var src = new global::app.@this("/src");
        var vars = src.User.Context.Variable;
        vars.Set("user", "alice");        // user var — survives
        vars.Set("!myInfra", "infra");    // !-prefixed — skipped

        var snap = src.Snapshot();
        var captured = snap.Section("Variables").Read<List<Data>>("variables");
        await Assert.That(captured).IsNotNull();

        var names = captured!.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await Assert.That(names.Contains("user")).IsTrue();
        await Assert.That(names.Any(n => n.StartsWith("!"))).IsFalse();
        await Assert.That(names.Contains("Now")).IsFalse();
        await Assert.That(names.Contains("NowUtc")).IsFalse();
        await Assert.That(names.Contains("GUID")).IsFalse();
        await Assert.That(names.Contains("Settings")).IsFalse();
    }
}
