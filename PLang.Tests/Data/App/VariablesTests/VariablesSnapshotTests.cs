namespace PLang.Tests.App.VariablesTests;

public class VariablesSnapshotTests
{
    [Test]
    public async Task Variables_RoundTrip_PreservesValuesAndProperties_ForUserVars()
    {
        // Set %x%=1 and %obj%={a:1}; Capture; Restore into fresh Variables; deep-equal.
        var src = global::PLang.Tests.TestApp.Create("/src");
        src.User.Context.Variable.Set("x", 1);
        src.User.Context.Variable.Set("obj", new Dictionary<string, object?> { ["a"] = 1 });

        var snap = src.Snapshot();
        var dst = global::PLang.Tests.TestApp.Create("/dst");
        dst.Restore(snap, dst.User.Context);

        var x = await dst.User.Context.Variable.Get("x");
        await Assert.That(x).IsNotNull();
        await Assert.That((await x!.Value())?.ToString()).IsEqualTo("1");

        // The dict round-trips as a native dict value — read its key the plang way,
        // not by casting to a raw CLR IDictionary.
        var obj = await dst.User.Context.Variable.Get("obj");
        await Assert.That(obj).IsNotNull();
        var dict = (await obj!.Value()) as global::app.type.dict.@this;
        await Assert.That(dict).IsNotNull();
        await Assert.That(dict!.Get("a")?.Peek()?.ToString()).IsEqualTo("1");
    }

    [Test]
    public async Task Variables_Snapshot_ExcludesBangPrefixedAndDynamicData()
    {
        // Existing partition: skip !-prefix, DynamicData (Now/GUID/!app/MyIdentity).
        // Settings is now a navigable resolver (not in _variables) so it's absent
        // by construction — no special-case needed.
        var src = global::PLang.Tests.TestApp.Create("/src");
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
