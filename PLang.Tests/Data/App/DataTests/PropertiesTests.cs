namespace PLang.Tests.App.DataTests;

// Stage 4 of data-serialize-cleanup rewrote Properties from IList<Data> to
// Dictionary<string, object?> of wire-supported primitives. The exhaustive
// per-method coverage that lived here for the old IList surface is replaced
// by:
//   - PLang.Tests.App.Serialization.PropertiesWireShapeTests — wire emission
//   - PLang.Tests.App.Serialization.IntegrationCuts.Cut4_PropertiesWireTests
//
// The smoke tests below pin the new API's basic correctness; the wire-shape
// and rejection semantics are covered in those Stage-4 suites.

public class PropertiesTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/PropertiesTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test] public async Task Set_StringValue_RoundTrips()
    {
        var data = _app.Data("x", 0);
        data.Properties["key"] = "value";
        await Assert.That((await data.Properties.Value("key"))).IsEqualTo("value");
    }

    [Test] public async Task Set_IntValue_RoundTrips()
    {
        var data = _app.Data("x", 0);
        data.Properties["n"] = 42;
        await Assert.That((await data.Properties.Value("n"))).IsEqualTo(42);
    }

    [Test] public async Task SettingNull_RemovesKey()
    {
        var data = _app.Data("x", 0);
        data.Properties["k"] = "v";
        data.Properties["k"] = null;
        await Assert.That(data.Properties.ContainsKey("k")).IsFalse();
    }

    [Test] public async Task UnknownKey_ReturnsNull()
    {
        var data = _app.Data("x", 0);
        await Assert.That((await data.Properties.Value("missing"))).IsNull();
    }

    [Test] public async Task DataInstanceAsValue_Rejected()
    {
        var data = _app.Data("x", 0);
        var inner = _app.Data("y", 1);
        await Assert.That(() => data.Properties["k"] = inner).Throws<ArgumentException>();
    }

    [Test] public async Task UnsupportedType_Rejected()
    {
        var data = _app.Data("x", 0);
        await Assert.That(() => data.Properties["k"] = new System.Threading.CancellationTokenSource()).Throws<ArgumentException>();
    }

    [Test] public async Task Clone_DeepCopiesEntries()
    {
        var props = new global::app.data.Properties();
        props["k"] = "v";
        var clone = props.Clone();
        clone["k"] = "w";
        await Assert.That(await props.Value("k")).IsEqualTo("v");
        await Assert.That(await clone.Value("k")).IsEqualTo("w");
    }
}
