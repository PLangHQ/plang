namespace PLang.Tests.App.VariablesTests;

public class VariablesSnapshotTests
{
    [Test]
    public async Task Variables_RoundTrip_PreservesValuesAndProperties_ForUserVars()
    {
        // Set %x%=1 and %obj%={a:1}; Capture; Restore into fresh Variables; deep-equal.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Variables_Snapshot_ExcludesBangPrefixed_DynamicData_AndSettingsVariables()
    {
        // Existing partition: skip !-prefix, DynamicData (Now/GUID/!app/MyIdentity),
        // SettingsVariable (sqlite-backed). All must be absent from captured payload.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
